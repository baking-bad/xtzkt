using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class DelegatorCycleCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, Cycle? futureCycle)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            await CreateFromSnapshots(futureCycle!);

            #region weird delegators
            if (block.Cycle > 0)
            {
                //one-way change...
                await Db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM "DelegatorCycles" as dc
                    USING "Addresses" as acc
                    WHERE acc."Id" = dc."BakerId"
                    AND dc."ChainId" = {0}
                    AND dc."Cycle" = {1}
                    AND acc."Type" != {2}
                    """, block.ChainId, block.Cycle - 1, (int)AddressType.L1Baker);
            }
            #endregion
        }

        public virtual async Task Revert(L1Block block)
        {
            if (block.Events.HasFlag(L1BlockEvents.CycleBegin))
            {
                var futureCycle = block.Cycle + Context.Protocol.ConsensusRightsDelay;

                await Db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM "DelegatorCycles"
                    WHERE "ChainId" = {0}
                    AND "Cycle" = {1}
                    """, block.ChainId, futureCycle);
            }
        }

        protected virtual Task CreateFromSnapshots(Cycle futureCycle)
        {
            return Db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "DelegatorCycles" (
                    "ChainId",
                    "Cycle",
                    "DelegatorId",
                    "BakerId",
                    "DelegatedBalance",
                    "StakedPseudotokens"
                )
                SELECT
                    {0},
                    {1},
                    "AddressId",
                    "BakerId",
                    "OwnDelegatedBalance",
                    "Pseudotokens"
                FROM "SnapshotBalances"
                WHERE "ChainId" = {0}
                AND "Level" = {2}
                AND "BakerId" != "AddressId"
                """, futureCycle.ChainId, futureCycle.Index, futureCycle.SnapshotLevel);
        }
    }
}
