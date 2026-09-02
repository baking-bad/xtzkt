using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto03
{
    class BallotsCommit : ProtocolCommit
    {
        public BallotsCommit(ProtocolHandler protocol) : base(protocol) { }

        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var period = await Cache.Periods.GetAsync(content.RequiredInt32("period"));
            var proposal = await Cache.Proposals.GetAsync(period.Epoch, content.RequiredString("proposal"));
            var sender = Cache.Addresses.GetExistingBaker(content.RequiredString("source"));

            var snapshot = await Db.VotingSnapshots
                .FirstOrDefaultAsync(x => x.ChainId == block.ChainId && x.Period == period.Index && x.BakerId == sender.Id)
                    ?? throw new ValidationException("Ballot sender is not on the voters list");

            var ballot = new BallotOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                SenderId = sender.Id,
                VotingPower = snapshot.VotingPower,
                Epoch = period.Epoch,
                Period = period.Index,
                ProposalId = proposal.Id,
                Vote = content.RequiredString("ballot") switch
                {
                    "yay" => Vote.Yay,
                    "nay" => Vote.Nay,
                    "pass" => Vote.Pass,
                    _ => throw new Exception("invalid ballot value")
                }
            };
            #endregion

            #region entities
            //Db.TryAttach(block);
            Db.TryAttach(period);
            Db.TryAttach(proposal);
            Db.TryAttach(sender);
            //Db.TryAttach(snapshot);
            #endregion

            #region apply operation
            if (ballot.Vote == Vote.Yay)
            {
                period.YayBallots++;
                period.YayVotingPower += ballot.VotingPower;
                snapshot.Status = VoterStatus.VotedYay;
            }
            else if (ballot.Vote == Vote.Nay)
            {
                period.NayBallots++;
                period.NayVotingPower += ballot.VotingPower;
                snapshot.Status = VoterStatus.VotedNay;
            }
            else
            {
                period.PassBallots++;
                period.PassVotingPower += ballot.VotingPower;
                snapshot.Status = VoterStatus.VotedPass;
            }

            sender.BallotsCount++;

            block.Operations |= L1Operations.Ballot;

            Cache.Chain.Get().BallotOpsCount++;
            #endregion

            Db.BallotOps.Add(ballot);
            Context.BallotOps.Add(ballot);
        }

        public virtual async Task Revert(L1Block block, BallotOperation ballot)
        {
            #region entities
            var sender = Cache.Addresses.GetBaker(ballot.SenderId);

            var snapshot = await Db.VotingSnapshots
                .FirstAsync(x => x.ChainId == block.ChainId && x.Period == ballot.Period && x.BakerId == ballot.SenderId);

            var period = await Cache.Periods.GetAsync(ballot.Period);

            Db.TryAttach(sender);
            Db.TryAttach(period);
            #endregion

            #region revert operation
            if (ballot.Vote == Vote.Yay)
            {
                period.YayBallots--;
                period.YayVotingPower -= ballot.VotingPower;
                snapshot.Status = VoterStatus.None;
            }
            else if (ballot.Vote == Vote.Nay)
            {
                period.NayBallots--;
                period.NayVotingPower -= ballot.VotingPower;
                snapshot.Status = VoterStatus.None;
            }
            else
            {
                period.PassBallots--;
                period.PassVotingPower -= ballot.VotingPower;
                snapshot.Status = VoterStatus.None;
            }

            sender.BallotsCount--;

            Cache.Chain.Get().BallotOpsCount--;
            #endregion

            Db.BallotOps.Remove(ballot);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
