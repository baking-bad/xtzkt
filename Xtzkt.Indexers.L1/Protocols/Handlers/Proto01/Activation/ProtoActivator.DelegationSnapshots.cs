using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        public void BootstrapDelegationSnapshots(List<L1Address> addresses)
        {
            Db.DelegationSnapshots.AddRange(addresses
                .Where(x => x.Staked)
                .Select(x => new DelegationSnapshot
                {
                    ChainId = x.ChainId,
                    Level = 1,
                    BakerId = x.BakerId ?? x.Id,
                    AddressId = x.Id,

                    OwnDelegatedBalance = x.Balance - ((x as L1Baker)?.OwnStakedBalance ?? 0),
                    ExternalDelegatedBalance = (x as L1Baker)?.ExternalDelegatedBalance,
                    DelegatorsCount = (x as L1Baker)?.DelegatorsCount,

                    PrevMinTotalDelegatedLevel = (x as L1Baker)?.MinTotalDelegatedLevel,
                    PrevMinTotalDelegated = (x as L1Baker)?.MinTotalDelegated
                }));
        }

        public async Task ClearDelegationSnapshots()
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "DelegationSnapshots"
                WHERE "ChainId" = {0}
                """, Cache.Chain.Get().Id);
        }
    }
}
