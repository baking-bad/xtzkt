using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        public void BootstrapVoting(L1Protocol protocol, List<L1Address> addresses)
        {
            var snapshots = addresses
                .Where(x => x.Type == AddressType.L1Baker)
                .OfType<L1Baker>()
                .Select(x => new VotingSnapshot
                {
                    ChainId = protocol.ChainId,
                    Level = 1,
                    Period = 0,
                    BakerId = x.Id,
                    VotingPower = x.VotingPower,
                    Status = VoterStatus.None
                });

            var period = new VotingPeriod
            {
                ChainId = protocol.ChainId,
                Index = 0,
                Epoch = 0,
                FirstLevel = 1,
                LastLevel = protocol.BlocksPerVoting,
                Kind = PeriodKind.Proposal,
                Status = PeriodStatus.Active,
                TotalBakers = snapshots.Count(),
                TotalVotingPower = snapshots.Sum(x => x.VotingPower),
                UpvotesQuorum = protocol.ProposalQuorum,
                ProposalsCount = 0,
                TopUpvotes = 0,
                TopVotingPower = 0,
                SingleWinner = false
            };

            Db.VotingSnapshots.AddRange(snapshots);
            Db.VotingPeriods.Add(period);
            Cache.Periods.Add(period);
        }

        public async Task ClearVoting()
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "VotingPeriods" WHERE "ChainId" = {0};
                DELETE FROM "VotingSnapshots" WHERE "ChainId" = {0};
                """, Cache.Chain.Get().Id);
            Cache.Periods.Reset();
        }
    }
}
