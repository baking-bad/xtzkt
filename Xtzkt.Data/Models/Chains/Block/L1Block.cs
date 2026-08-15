using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class L1Block() : Block(Layer.L1)
{
    public required int Cycle { get; set; }

    public int? SoftwareId { get; set; }

    public int PayloadRound { get; set; }
    public int BlockRound { get; set; }
    public long AttestationPower { get; set; }
    public long AttestationCommittee { get; set; }

    [Column(nameof(Events))]
    public L1BlockEvents Events { get; set; }

    [Column(nameof(Operations))]
    public L1Operations Operations { get; set; }

    public long RewardDelegated { get; set; }
    public long RewardStakedOwn { get; set; }
    public long RewardStakedEdge { get; set; }
    public long RewardStakedShared { get; set; }
    public long BonusDelegated { get; set; }
    public long BonusStakedOwn { get; set; }
    public long BonusStakedEdge { get; set; }
    public long BonusStakedShared { get; set; }

    [Column(nameof(BakerFees))]
    public long BakerFees { get; set; }

    [Column(nameof(BurnedFees))]
    public long BurnedFees { get; set; }

    [Column(nameof(ProposerId))]
    public int? ProposerId { get; set; }
    public int? ProducerId { get; set; }
    public long? RevelationId { get; set; }
    public int? ResetBakerDeactivation { get; set; }
    public int? ResetProposerDeactivation { get; set; }

    public bool? LBToggle { get; set; }
    public int LBToggleEma { get; set; }
}

[Flags]
public enum L1BlockEvents
{
    None                    = AllBlockEvents.None,

    CycleBegin              = AllBlockEvents.CycleBegin,
    CycleEnd                = AllBlockEvents.CycleEnd,
    ProtocolBegin           = AllBlockEvents.ProtocolBegin,
    ProtocolEnd             = AllBlockEvents.ProtocolEnd,

    Deactivations           = AllBlockEvents.Deactivations,
    BalanceSnapshot         = AllBlockEvents.BalanceSnapshot,
    DelegationSnapshot      = AllBlockEvents.DelegationSnapshot,
    DoubleBakingSlashing    = AllBlockEvents.DoubleBakingSlashing,
    DoubleConsensusSlashing = AllBlockEvents.DoubleConsensusSlashing,

    NewAddresses            = AllBlockEvents.NewAddresses,
    Bigmaps                 = AllBlockEvents.Bigmaps,
    Tokens                  = AllBlockEvents.Tokens,
    Events                  = AllBlockEvents.Events,
    Tickets                 = AllBlockEvents.Tickets,
}

[Flags]
public enum L1Operations : long
{
    None                    = AllOperations.None,

    Activation              = AllOperations.Activation,
    DalEntrapmentEvidence   = AllOperations.DalEntrapmentEvidence,
    DoubleBaking            = AllOperations.DoubleBaking,
    DoubleConsensus         = AllOperations.DoubleConsensus,
    DrainDelegate           = AllOperations.DrainDelegate,
    NonceRevelation         = AllOperations.NonceRevelation,
    VdfRevelation           = AllOperations.VdfRevelation,

    Attestation             = AllOperations.Attestation,
    Preattestation          = AllOperations.Preattestation,

    Ballot                  = AllOperations.Ballot,
    Proposal                = AllOperations.Proposal,

    Migration               = AllOperations.Migration,
    AttestationRewards      = AllOperations.AttestationRewards,
    Autostaking             = AllOperations.Autostaking,
    DalAttestationReward    = AllOperations.DalAttestationReward,
    RevelationPenalty       = AllOperations.RevelationPenalty,
    Subsidy                 = AllOperations.Subsidy,

    Origination             = AllOperations.Origination,
    Transaction             = AllOperations.Transaction,
    DalPublishCommitment    = AllOperations.DalPublishCommitment,
    Delegation              = AllOperations.Delegation,
    IncreasePaidStorage     = AllOperations.IncreasePaidStorage,
    RegisterConstant        = AllOperations.RegisterConstant,
    Reveal                  = AllOperations.Reveal,
    SetDepositsLimits       = AllOperations.SetDepositsLimits,
    SetDelegateParameters   = AllOperations.SetDelegateParameters,
    SmartRollupAddMessages  = AllOperations.SmartRollupAddMessages,
    SmartRollupCement       = AllOperations.SmartRollupCement,
    SmartRollupExecute      = AllOperations.SmartRollupExecute,
    SmartRollupOriginate    = AllOperations.SmartRollupOriginate,
    SmartRollupPublish      = AllOperations.SmartRollupPublish,
    SmartRollupRecoverBond  = AllOperations.SmartRollupRecoverBond,
    SmartRollupRefute       = AllOperations.SmartRollupRefute,
    Staking                 = AllOperations.Staking,
    TransferTicket          = AllOperations.TransferTicket,
    UpdateSecondaryKey      = AllOperations.UpdateSecondaryKey,
}

public static class L1BlockModel
{
    public static void BuildL1BlockModel(this ModelBuilder modelBuilder)
    {
    }
}
