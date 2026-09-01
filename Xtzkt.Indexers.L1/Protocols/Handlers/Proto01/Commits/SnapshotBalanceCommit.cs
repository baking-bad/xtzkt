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
                var bakerIds = new List<int>();
                var addressIds = new List<int>();
                var ownDelegated = new List<long>();
                var externalDelegated = new List<long?>();
                var delegatorsCounts = new List<int?>();

                foreach (var row in deactivated)
                {
                    bakerIds.Add(row.baker.Id);
                    addressIds.Add(row.baker.Id);
                    ownDelegated.Add(row.baker.OwnDelegatedBalance);
                    externalDelegated.Add(row.baker.ExternalDelegatedBalance);
                    delegatorsCounts.Add(row.baker.DelegatorsCount);

                    foreach (var delegator in row.delegators)
                    {
                        bakerIds.Add(delegator.BakerId!.Value);
                        addressIds.Add(delegator.Id);
                        ownDelegated.Add(delegator.Balance);
                        externalDelegated.Add(null);
                        delegatorsCounts.Add(null);
                    }
                }

                if (bakerIds.Count > 0)
                {
                    await Db.Database.ExecuteSqlRawAsync("""
                        INSERT INTO "SnapshotBalances" (
                            "ChainId",
                            "Level",
                            "BakerId",
                            "AddressId",
                            "OwnDelegatedBalance",
                            "ExternalDelegatedBalance",
                            "DelegatorsCount"
                        )
                        SELECT {0}, {1}, q.baker, q.address, q.own, q.external, q.delegators
                        FROM unnest({2}::int[], {3}::int[], {4}::bigint[], {5}::bigint[], {6}::int[])
                        AS q(baker, address, own, external, delegators)
                        """,
                        block.ChainId, block.Level,
                        bakerIds, addressIds, ownDelegated, externalDelegated, delegatorsCounts);
                }
            }
        }

        protected virtual async Task SubtractCycleRewards(JsonElement rawBlock, L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            var bakerIds = new List<int>();
            var values = new List<long>();

            foreach (var updates in GetBalanceUpdates(rawBlock)
                .Where(x => x.RequiredString("kind")[0] == 'f' &&
                            x.RequiredString("category")[0] == 'r' &&
                            x.RequiredInt64("change") < 0 &&
                            GetFreezerCycle(x) != block.Cycle)
                .Select(x => (x.RequiredString("delegate"), x.RequiredInt64("change")))
                .GroupBy(x => x.Item1))
            {
                bakerIds.Add(Cache.Addresses.GetExistingBaker(updates.Key).Id);
                values.Add(updates.Sum(x => -x.Item2));
            }

            if (bakerIds.Count > 0)
            {
                await Db.Database.ExecuteSqlRawAsync("""
                    UPDATE "SnapshotBalances" as sb
                    SET "OwnDelegatedBalance" = "OwnDelegatedBalance" - reward.value
                    FROM unnest({2}::int[], {3}::bigint[]) as reward(baker, value)
                    WHERE sb."ChainId" = {0}
                    AND sb."Level" = {1}
                    AND sb."BakerId" = reward.baker
                    AND sb."AddressId" = reward.baker
                    """,
                    block.ChainId, block.Level, bakerIds, values);
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
                var bakerIds = new List<int>();
                var addressIds = new List<int>();
                var ownDelegated = new List<long>();
                var externalDelegated = new List<long?>();
                var delegatorsCounts = new List<int?>();

                foreach (var weirds in weirdOriginations.Where(weirds => weirds.Sum(x => x.contract.Balance) >= protocol.MinimalStake))
                {
                    bakerIds.Add(weirds.Key);
                    addressIds.Add(weirds.Key);
                    ownDelegated.Add(0);
                    externalDelegated.Add(weirds.Sum(x => x.contract.Balance));
                    delegatorsCounts.Add(weirds.Count());

                    foreach (var weird in weirds)
                    {
                        bakerIds.Add(weirds.Key);
                        addressIds.Add(weird.contract.Id);
                        ownDelegated.Add(weird.contract.Balance);
                        externalDelegated.Add(null);
                        delegatorsCounts.Add(null);
                    }
                }

                if (bakerIds.Count > 0)
                {
                    await Db.Database.ExecuteSqlRawAsync("""
                        INSERT INTO "SnapshotBalances" (
                            "ChainId",
                            "Level",
                            "BakerId",
                            "AddressId",
                            "OwnDelegatedBalance",
                            "ExternalDelegatedBalance",
                            "DelegatorsCount"
                        )
                        SELECT {0}, {1}, q.baker, q.address, q.own, q.external, q.delegators
                        FROM unnest({2}::int[], {3}::int[], {4}::bigint[], {5}::bigint[], {6}::int[])
                        AS q(baker, address, own, external, delegators)
                        """,
                        block.ChainId, block.Level,
                        bakerIds, addressIds, ownDelegated, externalDelegated, delegatorsCounts);
                }
            }
        }
    }
}
