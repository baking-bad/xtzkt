using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class DelegationSnapshotCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task Apply()
        {
            if (Context.Block.Events.HasFlag(L1BlockEvents.CycleBegin))
            {
                await Db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM "DelegationSnapshots"
                    WHERE "ChainId" = {0}
                    AND "Level" < {1}
                    """, Context.Block.ChainId, Context.Block.Level - Context.Protocol.BlocksPerCycle);

                await Db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "DelegationSnapshots" (
                        "ChainId",
                        "Level",
                        "BakerId",
                        "AddressId",
                        "OwnDelegatedBalance",
                        "ExternalDelegatedBalance",
                        "DelegatorsCount",
                        "PrevMinTotalDelegatedLevel",
                        "PrevMinTotalDelegated"
                    )

                    SELECT
                        {0},
                        {1},
                        "Id",
                        "Id",
                        "OwnDelegatedBalance",
                        "ExternalDelegatedBalance",
                        "DelegatorsCount",
                        "MinTotalDelegatedLevel",
                        "MinTotalDelegated"
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "Type" = {2}

                    UNION ALL
                    
                    SELECT
                        {0},
                        {1},
                        "BakerId",
                        "Id",
                        "Balance" - (CASE
                                     WHEN "UnstakedBakerId" IS NOT NULL AND  "UnstakedBakerId" != "BakerId"
                                     THEN "UnstakedBalance"
                                     ELSE 0
                                     END),
                        NULL::bigint,
                        NULL::integer,
                        NULL::integer,
                        NULL::bigint
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "Type" != {2}
                    AND "BakerId" IS NOT NULL
                    
                    UNION ALL
                    
                    SELECT
                        {0},
                        {1},
                        "UnstakedBakerId",
                        "Id",
                        "UnstakedBalance",
                        NULL::bigint,
                        NULL::integer,
                        NULL::integer,
                        NULL::bigint
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "UnstakedBakerId" IS NOT NULL
                    AND "UnstakedBakerId" IS DISTINCT FROM "BakerId"
                    AND "UnstakedBakerId" != "Id"
                    """, Context.Block.ChainId, Context.Block.Level, (int)AddressType.L1Baker);

                foreach (var baker in Cache.Addresses.GetBakers())
                {
                    baker.MinTotalDelegated = baker.TotalDelegated;
                    baker.MinTotalDelegatedLevel = Context.Block.Level;
                }

                await Db.Database.ExecuteSqlRawAsync("""
                    UPDATE "Addresses"
                    SET "MinTotalDelegated" = "OwnDelegatedBalance" + "ExternalDelegatedBalance",
                        "MinTotalDelegatedLevel" = {0}
                    WHERE "ChainId" = {1}
                    AND "Type" = {2}
                    """, Context.Block.Level, Context.Block.ChainId, (int)AddressType.L1Baker);

                await SetBlockEvent();
            }
            else if (Cache.Addresses.GetBakers().Any(x => x.TotalDelegated < x.MinTotalDelegated))
            {
                var bakers = Cache.Addresses.GetBakers()
                    .Where(x => x.TotalDelegated < x.MinTotalDelegated)
                    .ToList();

                var ids = bakers.Select(x => x.Id).ToList();

                await Db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "DelegationSnapshots" (
                        "ChainId",
                        "Level",
                        "BakerId",
                        "AddressId",
                        "OwnDelegatedBalance",
                        "ExternalDelegatedBalance",
                        "DelegatorsCount",
                        "PrevMinTotalDelegatedLevel",
                        "PrevMinTotalDelegated"
                    )

                    SELECT
                        {0},
                        {1},
                        "Id",
                        "Id",
                        "OwnDelegatedBalance",
                        "ExternalDelegatedBalance",
                        "DelegatorsCount",
                        "MinTotalDelegatedLevel",
                        "MinTotalDelegated"
                    FROM "Addresses"
                    WHERE "Id" = ANY({2})

                    UNION ALL
                    
                    SELECT
                        {0},
                        {1},
                        "BakerId",
                        "Id",
                        "Balance" - (CASE
                                     WHEN "UnstakedBakerId" IS NOT NULL AND "UnstakedBakerId" != "BakerId"
                                     THEN "UnstakedBalance"
                                     ELSE 0
                                     END),
                        NULL::bigint,
                        NULL::integer,
                        NULL::integer,
                        NULL::bigint
                    FROM "Addresses"
                    WHERE "BakerId" IS NOT NULL
                    AND "BakerId" = ANY({2})
                    AND "BakerId" != "Id"
                    
                    UNION ALL
                    
                    SELECT
                        {0},
                        {1},
                        "UnstakedBakerId",
                        "Id",
                        "UnstakedBalance",
                        NULL::bigint,
                        NULL::integer,
                        NULL::integer,
                        NULL::bigint
                    FROM "Addresses"
                    WHERE "UnstakedBakerId" IS NOT NULL
                    AND "UnstakedBakerId" = ANY({2})
                    AND "UnstakedBakerId" IS DISTINCT FROM "BakerId"
                    AND "UnstakedBakerId" != "Id"
                    """, Context.Block.ChainId, Context.Block.Level, ids);

                foreach (var baker in bakers)
                {
                    baker.MinTotalDelegated = baker.TotalDelegated;
                    baker.MinTotalDelegatedLevel = Context.Block.Level;
                }

                await Db.Database.ExecuteSqlRawAsync("""
                    UPDATE "Addresses"
                    SET "MinTotalDelegated" = "OwnDelegatedBalance" + "ExternalDelegatedBalance",
                        "MinTotalDelegatedLevel" = {0}
                    WHERE "Id" = ANY({1})
                    """, Context.Block.Level, ids);

                await SetBlockEvent();
            }
        }

        public async Task Revert()
        {
            if (!Context.Block.Events.HasFlag(L1BlockEvents.DelegationSnapshot))
                return;

            var bakerSnapshots = await Db.DelegationSnapshots
                .AsNoTracking()
                .Where(x => x.ChainId == Context.Block.ChainId && x.Level == Context.Block.Level && x.BakerId == x.AddressId)
                .ToListAsync();

            foreach (var snapshot in bakerSnapshots)
            {
                var baker = Cache.Addresses.GetBaker(snapshot.BakerId);
                baker.MinTotalDelegated = snapshot.PrevMinTotalDelegated!.Value;
                baker.MinTotalDelegatedLevel = snapshot.PrevMinTotalDelegatedLevel!.Value;
            }

            await Db.Database.ExecuteSqlRawAsync("""
                UPDATE "Addresses" AS baker
                SET "MinTotalDelegated" = snapshot."PrevMinTotalDelegated",
                    "MinTotalDelegatedLevel" = snapshot."PrevMinTotalDelegatedLevel"
                FROM (
                    SELECT "BakerId", "PrevMinTotalDelegatedLevel", "PrevMinTotalDelegated"
                    FROM "DelegationSnapshots"
                    WHERE "ChainId" = {0}
                    AND "Level" = {1}
                    AND "BakerId" = "AddressId"
                ) AS snapshot
                WHERE baker."Id" = snapshot."BakerId"
                """, Context.Block.ChainId, Context.Block.Level);

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "DelegationSnapshots"
                WHERE "ChainId" = {0}
                AND "Level" = {1}
                """, Context.Block.ChainId, Context.Block.Level);
        }

        async Task SetBlockEvent()
        {
            var block = Cache.Blocks.GetCached(Context.Block.Level);
            block.Events |= L1BlockEvents.DelegationSnapshot;

            await Db.Database.ExecuteSqlRawAsync("""
                UPDATE "Blocks"
                SET "Events" = "Events" | {0}
                WHERE "ChainId" = {1}
                AND "Level" = {2}
                """, L1BlockEvents.DelegationSnapshot, Cache.Chain.Get().Id, block.Level);
        }
    }
}
