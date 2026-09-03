using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class StakerCycleCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task Apply()
        {
            if (!Context.Block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            #region finalize
            await Db.Database.ExecuteSqlRawAsync("""
                UPDATE "StakerCycles"
                SET "FinalStake" = snapshot.stake
                FROM (
                    SELECT
                        sc."Id" AS id,
                        FLOOR(baker."ExternalStakedBalance"
                            * COALESCE(staker."StakedPseudotokens", 0::numeric)
                            / COALESCE(baker."IssuedPseudotokens", 1::numeric))::bigint AS stake
                    FROM "StakerCycles" AS sc
                    INNER JOIN "Addresses" AS baker ON baker."Id" = sc."BakerId"
                    INNER JOIN "Addresses" AS staker ON staker."Id" = sc."StakerId"
                    WHERE sc."ChainId" = {0}
                    AND sc."Cycle" = {1}
                ) AS snapshot
                WHERE "Id" = snapshot.id
                """, Context.Block.ChainId, Context.Block.Cycle - 1);
            #endregion

            #region create
            await Db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "StakerCycles" (
                    "ChainId",
                    "Cycle",
                    "BakerId",
                    "StakerId",
                    "InitialStake",
                    "AvgStake",
                    "AddedStake",
                    "RemovedStake",
                    "FinalStake"
                )
                SELECT
                    {0},
                    {1},
                    sc."BakerId",
                    sc."StakerId",
                    sc."FinalStake",
                    sc."FinalStake",
                    0,
                    0,
                    NULL
                FROM "StakerCycles" AS sc
                INNER JOIN "Addresses" AS staker ON staker."Id" = sc."StakerId"
                WHERE sc."ChainId" = {0} AND sc."Cycle" = {2} AND staker."StakedPseudotokens" IS NOT NULL
                """, Context.Block.ChainId, Context.Block.Cycle, Context.Block.Cycle - 1);
            #endregion
        }

        public async Task Revert()
        {
            if (!Context.Block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            #region revert create
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "StakerCycles"
                WHERE "ChainId" = {0}
                AND "Cycle" = {1}
                """, Context.Block.ChainId, Context.Block.Cycle);
            #endregion

            #region revert finalize
            await Db.Database.ExecuteSqlRawAsync("""
                UPDATE "StakerCycles"
                SET "FinalStake" = NULL
                WHERE "ChainId" = {0}
                AND "Cycle" = {1}
                """, Context.Block.ChainId, Context.Block.Cycle - 1);
            #endregion
        }
    }
}
