using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        public void BootstrapSnapshotBalances(List<L1Address> addresses)
        {
            Db.SnapshotBalances.AddRange(addresses
                .Where(x => x.Staked)
                .Select(x => new SnapshotBalance
                {
                    ChainId = x.ChainId,
                    Level = 1,
                    BakerId = x.BakerId ?? x.Id,
                    AddressId = x.Id,

                    OwnDelegatedBalance = x.Balance - ((x as L1Baker)?.OwnStakedBalance ?? 0),
                    ExternalDelegatedBalance = (x as L1Baker)?.ExternalDelegatedBalance,
                    DelegatorsCount = (x as L1Baker)?.DelegatorsCount,

                    OwnStakedBalance = (x as L1Baker)?.OwnStakedBalance,
                    ExternalStakedBalance = (x as L1Baker)?.ExternalStakedBalance,
                    StakersCount = (x as L1Baker)?.StakersCount,

                    Pseudotokens = (x as L1Baker)?.IssuedPseudotokens ?? (x as L1User)?.StakedPseudotokens
                }));
        }

        public async Task ClearSnapshotBalances()
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "SnapshotBalances"
                WHERE "ChainId" = {0}
                """, Cache.Chain.Get().Id);
        }
    }
}
