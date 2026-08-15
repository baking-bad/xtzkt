using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto05
{
    class VotingCommit(ProtocolHandler protocol) : Proto01.VotingCommit(protocol)
    {
        protected override int GetParticipationEma(VotingPeriod period, L1Protocol proto)
        {
            var prev = Db.VotingPeriods
                .AsNoTracking()
                .Where(x => x.ChainId == period.ChainId)
                .OrderByDescending(x => x.Index)
                .FirstOrDefault(x => x.Kind == PeriodKind.Exploration || x.Kind == PeriodKind.Promotion);

            if (prev != null)
            {
                var participation = 10000.MulRatio(prev.YayVotingPower!.Value + prev.NayVotingPower!.Value + prev.PassVotingPower!.Value, prev.TotalVotingPower);
                return (int)((prev.ParticipationEma!.Value * 8000 + participation * 2000) / 10000);
            }

            return proto.BallotQuorumMax;
        }

        protected override int GetBallotQuorum(VotingPeriod period, L1Protocol proto)
        {
            return proto.BallotQuorumMin + period.ParticipationEma!.Value * (proto.BallotQuorumMax - proto.BallotQuorumMin) / 10000;
        }
    }
}
