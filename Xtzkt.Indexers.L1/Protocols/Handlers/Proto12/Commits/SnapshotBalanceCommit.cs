using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class SnapshotBalanceCommit : Proto09.SnapshotBalanceCommit
    {
        public SnapshotBalanceCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override async Task SubtractCycleRewards(JsonElement rawBlock, L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            await Db.Database.ExecuteSqlRawAsync("""
                UPDATE "SnapshotBalances" as sb
                SET "OwnDelegatedBalance" = "OwnDelegatedBalance" - bc."AttestationRewardsDelegated"	                        
                FROM (
                    SELECT "BakerId", "AttestationRewardsDelegated"
                    FROM "BakerCycles"
                    WHERE "ChainId" = {0}
                    AND "Cycle" = {1}
                    AND "AttestationRewardsDelegated" != 0
                ) as bc
                WHERE sb."ChainId" = {0}
                AND sb."Level" = {2}
                AND sb."BakerId" = bc."BakerId"
                AND sb."AddressId" = bc."BakerId"
                """, block.ChainId, block.Cycle, block.Level);
        }
    }
}
