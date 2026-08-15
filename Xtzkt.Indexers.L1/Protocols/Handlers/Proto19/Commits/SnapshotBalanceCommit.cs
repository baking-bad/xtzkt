using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class SnapshotBalanceCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task Apply()
        {
            if (!Context.Block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "SnapshotBalances"
                WHERE "ChainId" = {0}
                AND "Level" < {1}
                """, Context.Block.ChainId, Context.Block.Level - Context.Protocol.BlocksPerCycle);

            await Db.Database.ExecuteSqlRawAsync("""
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
                    0,
                    0,
                    0,
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
                    0,
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
                AND "StakedPseudotokens" IS NOT NULL
                """, Context.Block.ChainId, Context.Block.Level, (int)AddressType.L1Baker);

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
                    
                SELECT
                    {0},
                    {1},
                    ds."BakerId",
                    ds."AddressId",
                    ds."OwnDelegatedBalance",
                    ds."ExternalDelegatedBalance",
                    ds."DelegatorsCount"
                FROM "Addresses" AS baker
                INNER JOIN "DelegationSnapshots" AS ds
                ON ds."ChainId" = {0} AND ds."Level" = baker."MinTotalDelegatedLevel" AND ds."BakerId" = baker."Id"
                WHERE baker."ChainId" = {0}
                AND baker."Staked" = true
                AND baker."Type" = {2}
                    
                ON CONFLICT ("ChainId", "Level", "BakerId", "AddressId")
                DO UPDATE
                SET
                    "OwnDelegatedBalance" = EXCLUDED."OwnDelegatedBalance",
                    "ExternalDelegatedBalance" = EXCLUDED."ExternalDelegatedBalance",
                    "DelegatorsCount" = EXCLUDED."DelegatorsCount"
                """, Context.Block.ChainId, Context.Block.Level, (int)AddressType.L1Baker);
        }

        public async Task Revert()
        {
            if (!Context.Block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "SnapshotBalances"
                WHERE "ChainId" = {0}
                AND "Level" = {1}
                """, Context.Block.ChainId, Context.Block.Level);
        }
    }
}
