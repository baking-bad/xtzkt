using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class SnapshotBalanceCommit(ProtocolHandler protocol) : Proto12.SnapshotBalanceCommit(protocol)
    {
        protected override Task TakeSnapshot(L1Block block)
        {
            return Db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "SnapshotBalances" (
                    "ChainId",
                    "Level",
                    "BakerId",
                    "AddressId",
                    "OwnDelegatedBalance",
                    "ExternalDelegatedBalance",
                    "DelegatorsCount",
                    "OwnStakedBalance",
                    "ExternalStakedBalance",
                    "StakersCount",
                    "Pseudotokens"
                )
                
                SELECT
                    {0},
                    {1},
                    "Id",
                    "Id",
                    "OwnDelegatedBalance",
                    "ExternalDelegatedBalance",
                    "DelegatorsCount",
                    "OwnStakedBalance",
                    "ExternalStakedBalance",
                    "StakersCount",
                    "IssuedPseudotokens"
                FROM "Addresses"
                WHERE "ChainId" = {0}
                AND "Staked" = true
                AND "Type" = {2}
                
                UNION ALL

                SELECT
                    {0},
                    {1},
                    "BakerId",
                    "Id",
                    "Balance" - (CASE
                                 WHEN "UnstakedBakerId" IS NOT NULL
                                 AND  "UnstakedBakerId" != "BakerId"
                                 THEN "UnstakedBalance"
                                 ELSE 0
                                 END),
                    NULL::bigint,
                    NULL::integer,
                    NULL::bigint,
                    NULL::bigint,
                    NULL::integer,
                    "StakedPseudotokens"
                FROM "Addresses"
                WHERE "ChainId" = {0}
                AND "Staked" = true
                AND "Type" != {2}

                UNION ALL
                
                SELECT
                    {0},
                    {1},
                    address."UnstakedBakerId",
                    address."Id",
                    address."UnstakedBalance",
                    NULL::bigint,
                    NULL::integer,
                    NULL::bigint,
                    NULL::bigint,
                    NULL::integer,
                    NULL::numeric
                FROM "Addresses" as address
                INNER JOIN "Addresses" as unstakedBaker
                ON unstakedBaker."Id" = address."UnstakedBakerId"
                WHERE address."ChainId" = {0}
                AND address."UnstakedBakerId" IS NOT NULL
                AND address."UnstakedBakerId" IS DISTINCT FROM address."BakerId"
                AND address."UnstakedBakerId" != address."Id"
                AND unstakedBaker."Staked" = true
                """, block.ChainId, block.Level, (int)AddressType.L1Baker);
        }

        protected override async Task TakeDeactivatedSnapshot(L1Block block)
        {
            var deactivated = await Db.Addresses
                .AsNoTracking()
                .OfType<L1Baker>()
                .Where(x => x.ChainId == block.ChainId && x.Type == AddressType.L1Baker)
                .Where(x => x.DeactivationLevel == block.Level)
                .ToListAsync();

            if (deactivated.Count > 0)
            {
                var values = new List<string>();
                foreach (var baker in deactivated)
                {
                    var delegators = await Db.Addresses
                        .OfType<L1Address>()
                        .Where(x => x.BakerId != null && x.BakerId == baker.Id)
                        .ToListAsync();

                    var unstakers = baker.ExternalUnstakedBalance > 0
                        ? await Db.Addresses
                            .OfType<L1User>()
                            .Where(x =>
                                x.UnstakedBakerId != null &&
                                x.UnstakedBakerId == baker.Id &&
                                x.UnstakedBakerId != x.BakerId &&
                                x.UnstakedBakerId != x.Id)
                            .ToListAsync()
                        : [];

                    values.Add("(" + string.Join(',',
                        block.ChainId,
                        block.Level,
                        baker.Id,
                        baker.Id,
                        baker.Balance - baker.OwnStakedBalance - (baker.UnstakedBakerId != null && baker.UnstakedBakerId != baker.Id ? baker.UnstakedBalance : 0),
                        baker.ExternalDelegatedBalance,
                        baker.DelegatorsCount,
                        baker.OwnStakedBalance,
                        baker.ExternalStakedBalance,
                        baker.StakersCount,
                        baker.IssuedPseudotokens ?? (object)"NULL::numeric") + ")");

                    foreach (var delegator in delegators)
                    {
                        values.Add("(" + string.Join(',',
                            block.ChainId,
                            block.Level,
                            delegator.BakerId,
                            delegator.Id,
                            delegator.Balance - (delegator is L1User user && user.UnstakedBakerId != null && user.UnstakedBakerId != user.BakerId ? user.UnstakedBalance : 0),
                            "NULL::bigint",
                            "NULL::integer",
                            "NULL::bigint",
                            "NULL::bigint",
                            "NULL::integer",
                            (delegator as L1User)?.StakedPseudotokens ?? (object)"NULL::numeric") + ")");
                    }

                    foreach (var unstaker in unstakers)
                    {
                        values.Add("(" + string.Join(',',
                            block.ChainId,
                            block.Level,
                            unstaker.UnstakedBakerId,
                            unstaker.Id,
                            unstaker.UnstakedBalance,
                            "NULL::bigint",
                            "NULL::integer",
                            "NULL::bigint",
                            "NULL::bigint",
                            "NULL::integer",
                            "NULL::numeric") + ")");
                    }
                }
                if (values.Count > 0)
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
                            "DelegatorsCount",
                            "OwnStakedBalance",
                            "ExternalStakedBalance",
                            "StakersCount",
                            "Pseudotokens"
                        )
                        VALUES
                        {string.Join(",\n", values)}
                        """);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
                }
            }
        }

        protected override async Task SubtractCycleRewards(JsonElement rawBlock, L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            await Db.Database.ExecuteSqlRawAsync("""
                UPDATE "SnapshotBalances" as sb
                SET 
                    "OwnDelegatedBalance" = "OwnDelegatedBalance" - bc."AttestationRewardsDelegated",
                    "OwnStakedBalance" = "OwnStakedBalance" - bc."AttestationRewardsStakedOwn" - bc."AttestationRewardsStakedEdge",
                    "ExternalStakedBalance" = "ExternalStakedBalance" - bc."AttestationRewardsStakedShared"
                FROM (
                    SELECT "BakerId", "AttestationRewardsDelegated", "AttestationRewardsStakedOwn", "AttestationRewardsStakedEdge", "AttestationRewardsStakedShared"
                    FROM "BakerCycles"
                    WHERE "ChainId" = {0}
                    AND "Cycle" = {1}
                    AND ("AttestationRewardsDelegated" != 0 OR "AttestationRewardsStakedOwn" != 0 OR "AttestationRewardsStakedEdge" != 0 OR "AttestationRewardsStakedShared" != 0)
                ) as bc
                WHERE sb."ChainId" = {0}
                AND sb."Level" = {2}
                AND sb."BakerId" = bc."BakerId"
                AND sb."AddressId" = bc."BakerId"
                """, block.ChainId, block.Cycle, block.Level);
        }
    }
}
