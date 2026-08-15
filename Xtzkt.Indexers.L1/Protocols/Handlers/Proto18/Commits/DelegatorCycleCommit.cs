using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class DelegatorCycleCommit : Proto03.DelegatorCycleCommit
    {
        public DelegatorCycleCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override Task CreateFromSnapshots(Cycle futureCycle)
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
