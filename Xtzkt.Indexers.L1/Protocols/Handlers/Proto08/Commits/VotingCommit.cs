using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto08
{
    class VotingCommit(ProtocolHandler protocol) : Proto05.VotingCommit(protocol)
    {
        // new voting period
        protected override ProposalStatus GetProposalStatus(Proposal proposal, VotingPeriod period)
        {
            if (period.Status == PeriodStatus.Success)
                return period.Kind == PeriodKind.Adoption
                    ? ProposalStatus.Accepted
                    : ProposalStatus.Active;

            if (period.Status == PeriodStatus.NoSupermajority)
                return ProposalStatus.Rejected;

            return ProposalStatus.Skipped;
        }

        // new voting period
        protected override VotingPeriod StartNextPeriod(L1Block block, L1Protocol protocol, VotingPeriod current)
        {
            return current.Kind switch
            {
                PeriodKind.Proposal => StartBallotPeriod(block, protocol, current, PeriodKind.Exploration),
                PeriodKind.Exploration => StartWaitingPeriod(block, protocol, current, PeriodKind.Testing),
                PeriodKind.Testing => StartBallotPeriod(block, protocol, current, PeriodKind.Promotion),
                PeriodKind.Promotion => StartWaitingPeriod(block, protocol, current, PeriodKind.Adoption),
                PeriodKind.Adoption => StartProposalPeriod(block, protocol, current),
                _ => throw new Exception("Invalid voting period kind")
            };
        }
    }
}
