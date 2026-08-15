using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class SnapshotBalanceCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(JsonElement rawBlock, L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.BalanceSnapshot))
                return;

            await TakeSnapshot(block);
            await TakeWeirdsSnapshot(block, Context.Protocol);
        }

        public virtual async Task Revert(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.BalanceSnapshot))
                return;

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "SnapshotBalances"
                WHERE "ChainId" = {0}
                AND "Level" = {1}
                """, block.ChainId, block.Level);
        }

        protected virtual Task TakeSnapshot(L1Block block)
        {
            return Db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "SnapshotBalances" (
                    "ChainId",
                    "Level",
                    "BakerId",
                    "AddressId",
                    "OwnDelegatedBalance",
                    "ExternalDelegatedBalance",
                    "DelegatorsCount"
                )
                SELECT
                    {0},
                    {1},
                    COALESCE("BakerId", "Id"),
                    "Id",
                    COALESCE("OwnDelegatedBalance", "Balance"),
                    "ExternalDelegatedBalance",
                    "DelegatorsCount"
                FROM "Addresses"
                WHERE "ChainId" = {0}
                AND "Staked" = true
                """, block.ChainId, block.Level);
        }

        protected Task RemoveOutdated(L1Block block, L1Protocol protocol)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return Task.CompletedTask;

            var level = block.Level - (protocol.ConsensusRightsDelay + 3) * protocol.BlocksPerCycle;
            return Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "SnapshotBalances"
                WHERE "ChainId" = {0}
                AND "Level" <= {1}
                """, block.ChainId, level);
        }

        protected virtual async Task TakeDeactivatedSnapshot(L1Block block)
        {
            var deactivated = await Db.Addresses
                .AsNoTracking()
                .Where(x => x.ChainId == block.ChainId && x.Type == AddressType.L1Baker)
                .GroupJoin(Db.Addresses, x => x.Id, x => (x as L1Address)!.BakerId, (baker, delegators) => new
                {
                    baker = (baker as L1Baker)!,
                    delegators = delegators as IEnumerable<L1Address> ?? Enumerable.Empty<L1Address>()
                })
                .Where(x => x.baker.DeactivationLevel == block.Level)
                .ToListAsync();

            if (deactivated.Count != 0)
            {
                var values = string.Join(",\n", deactivated
                    .SelectMany(row =>
                        new[] { $"({block.ChainId}, {block.Level}, {row.baker.Id}, {row.baker.Id}, {row.baker.OwnDelegatedBalance}, {row.baker.ExternalDelegatedBalance}, {row.baker.DelegatorsCount})" }
                        .Concat(row.delegators.Select(delegator => $"({block.ChainId}, {block.Level}, {delegator.BakerId}, {delegator.Id}, {delegator.Balance}, NULL::bigint, NULL::integer)"))));

                if (values.Length > 0)
                {
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
                    await Db.Database.ExecuteSqlRawAsync($"""
                        INSERT INTO "SnapshotBalances" (
                            "ChainId",
                            "Level",
                            "BakerId",
                            "AddressId",
                            "OwnDelegatedBalance",
                            "ExternalDelegatedBalance",
                            "DelegatorsCount"
                        )
                        VALUES
                        {values}
                        """);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
                }
            }
        }

        protected virtual async Task SubtractCycleRewards(JsonElement rawBlock, L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            var rewards = string.Join(",\n", GetBalanceUpdates(rawBlock)
                .Where(x => x.RequiredString("kind")[0] == 'f' &&
                            x.RequiredString("category")[0] == 'r' &&
                            x.RequiredInt64("change") < 0 &&
                            GetFreezerCycle(x) != block.Cycle)
                .Select(x => (x.RequiredString("delegate"), x.RequiredInt64("change")))
                .GroupBy(x => x.Item1)
                .Select(updates => $"({Cache.Addresses.GetExistingBaker(updates.Key).Id}, {updates.Sum(x => -x.Item2)}::bigint)"));

            if (rewards.Length > 0)
            {
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
                await Db.Database.ExecuteSqlRawAsync($"""
                    UPDATE "SnapshotBalances" as sb
                    SET "OwnDelegatedBalance" = "OwnDelegatedBalance" - reward.value
                    FROM (
                        VALUES
                        {rewards}
                    ) as reward(baker, value)
                    WHERE sb."ChainId" = {block.ChainId}
                    AND sb."Level" = {block.Level}
                    AND sb."BakerId" = reward.baker
                    AND sb."AddressId" = reward.baker
                    """);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
            }
        }

        protected virtual int GetFreezerCycle(JsonElement el)
        {
            return el.RequiredInt32("level");
        }

        protected virtual IEnumerable<JsonElement> GetBalanceUpdates(JsonElement rawBlock)
        {
            return rawBlock
                .GetProperty("metadata")
                .GetProperty("balance_updates")
                .EnumerateArray();
        }

        async Task TakeWeirdsSnapshot(L1Block block, L1Protocol protocol)
        {
            var weirdOriginations = (await Db.OriginationOps
                .AsNoTracking()
                .OfType<L1OriginationOperation>()
                .Join(Db.Addresses, x => x.BakerId, x => x.Id, (op, baker) => new { op, baker })
                .Join(Db.Addresses, x => x.op.ContractId, x => x.Id, (opBaker, contract) => new { opBaker.op, opBaker.baker, contract = (contract as L1Address)! })
                .Where(x =>
                    x.op.ChainId == block.ChainId &&
                    x.op.Status == OperationStatus.Applied &&
                    x.op.BakerId != null &&
                    x.baker.Type != AddressType.L1Baker &&
                    x.contract.BakerId == null)
                .ToListAsync())
                .GroupBy(x => x.baker.Id);

            if (weirdOriginations.Any())
            {
                var values = string.Join(",\n", weirdOriginations
                    .Where(weirds => weirds.Sum(x => x.contract.Balance) >= protocol.MinimalStake)
                    .SelectMany(weirds =>
                        new[] { $"({block.ChainId}, {block.Level}, {weirds.Key}, {weirds.Key}, 0, {weirds.Sum(x => x.contract.Balance)}, {weirds.Count()})" }
                        .Concat(weirds.Select(x => $"({block.ChainId}, {block.Level}, {weirds.Key}, {x.contract.Id}, {x.contract.Balance}, NULL::bigint, NULL::integer)"))));

                if (values.Length > 0)
                {
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
                    await Db.Database.ExecuteSqlRawAsync($"""
                        INSERT INTO "SnapshotBalances" (
                            "ChainId",
                            "Level",
                            "BakerId",
                            "AddressId",
                            "OwnDelegatedBalance",
                            "ExternalDelegatedBalance",
                            "DelegatorsCount"
                        )
                        VALUES
                        {values}
                        """);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
                }
            }
        }
    }
}
