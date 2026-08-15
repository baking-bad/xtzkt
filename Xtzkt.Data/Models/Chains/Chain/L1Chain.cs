using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class L1Chain() : Chain(Layer.L1)
{
    #region head
    public int Cycle { get; set; }
    public required string Protocol { get; set; }
    public required string NextProtocol { get; set; }
    public int VotingEpoch { get; set; }
    public int VotingPeriod { get; set; }
    #endregion

    #region state
    public int? AiActivationLevel { get; set; }
    public int? AbaActivationLevel { get; set; }
    public int PendingBakerParameters { get; set; }
    public int PendingSecondaryKeys { get; set; }
    #endregion

    #region counters
    public int ManagerCounter { get; set; }
    public int SmartRollupCommitmentCounter { get; set; }
    public int RefutationGameCounter { get; set; }
    public int InboxMessageCounter { get; set; }
    public int ProposalCounter { get; set; }
    public int SoftwareCounter { get; set; }
    #endregion

    #region entities count
    public int CommitmentsCount { get; set; }

    public int ActivationOpsCount { get; set; }
    public int BallotOpsCount { get; set; }
    public int DelegationOpsCount { get; set; }
    public int DalEntrapmentEvidenceOpsCount { get; set; }
    public int DoubleBakingOpsCount { get; set; }
    public int DoubleConsensusOpsCount { get; set; }
    public long AttestationOpsCount { get; set; }
    public int PreattestationOpsCount { get; set; }
    public int NonceRevelationOpsCount { get; set; }
    public int VdfRevelationOpsCount { get; set; }
    public int ProposalOpsCount { get; set; }
    public int StakingOpsCount { get; set; }
    public int SetDelegateParametersOpsCount { get; set; }
    public int AttestationRewardOpsCount { get; set; }
    public int DalAttestationRewardOpsCount { get; set; }
    public int SetDepositsLimitOpsCount { get; set; }

    public int UpdateSecondaryKeyOpsCount { get; set; }
    public int DrainDelegateOpsCount { get; set; }

    public int SubsidyOpsCount { get; set; }
    public int RevelationPenaltyOpsCount { get; set; }
    public int AutostakingOpsCount { get; set; }

    public int SmartRollupAddMessagesOpsCount { get; set; }
    public int SmartRollupCementOpsCount { get; set; }
    public int SmartRollupExecuteOpsCount { get; set; }
    public int SmartRollupOriginateOpsCount { get; set; }
    public int SmartRollupPublishOpsCount { get; set; }
    public int SmartRollupRecoverBondOpsCount { get; set; }
    public int SmartRollupRefuteOpsCount { get; set; }

    public int DalPublishCommitmentOpsCount { get; set; }

    public int CyclesCount { get; set; }
    public int StakingUpdatesCount { get; set; }
    public int UnstakeRequestsCount { get; set; }
    #endregion

    #region plugins
    public int QuoteLevel { get; set; }
    public double QuoteBtc { get; set; }
    public double QuoteEur { get; set; }
    public double QuoteUsd { get; set; }
    public double QuoteCny { get; set; }
    public double QuoteJpy { get; set; }
    public double QuoteKrw { get; set; }
    public double QuoteEth { get; set; }
    public double QuoteGbp { get; set; }

    public required string DomainsNameRegistry { get; set; }
    public int DomainsLevel { get; set; }
    #endregion 
}

public static class L1ChainModel
{
    public static void BuildL1ChainModel(this ModelBuilder modelBuilder)
    {
    }
}
