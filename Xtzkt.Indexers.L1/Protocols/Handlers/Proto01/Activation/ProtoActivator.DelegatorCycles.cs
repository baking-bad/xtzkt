using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        public void BootstrapDelegatorCycles(L1Protocol protocol, List<L1Address> addresses)
        {
            for (int cycle = 0; cycle <= protocol.ConsensusRightsDelay; cycle++)
            {
                Db.DelegatorCycles.AddRange(addresses
                    .Where(x => x.BakerId != null)
                    .Select(x => new DelegatorCycle
                    {
                        Id = 0,
                        ChainId = protocol.ChainId,
                        Cycle = cycle,
                        DelegatorId = x.Id,
                        BakerId = x.BakerId!.Value,
                        DelegatedBalance = x.Balance,
                        StakedPseudotokens = (x as L1User)?.StakedPseudotokens
                    }));
            }
        }

        public async Task ClearDelegatorCycles()
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "DelegatorCycles"
                WHERE "ChainId" = {0}
                """, Cache.Chain.Get().Id);
        }
    }
}
