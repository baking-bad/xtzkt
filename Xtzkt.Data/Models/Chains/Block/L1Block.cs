using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class L1Block() : Block(Layer.L1)
{
    [Column(Order = 21)]
    public required int Cycle { get; set; }

    [Column(Order = 24)]
    public int? SoftwareId { get; set; }

    [Column(Order = 25)]
    public int PayloadRound { get; set; }
    [Column(Order = 26)]
    public int BlockRound { get; set; }
    [Column(Order = 3)]
    public long AttestationPower { get; set; }
    [Column(Order = 4)]
    public long AttestationCommittee { get; set; }

    [Column(nameof(Events), Order = 27)]
    public L1BlockEvents Events { get; set; }

    [Column(nameof(Operations), Order = 2)]
    public L1Operations Operations { get; set; }

    [Column(Order = 5)]
    public long RewardDelegated { get; set; }
    [Column(Order = 6)]
    public long RewardStakedOwn { get; set; }
    [Column(Order = 7)]
    public long RewardStakedEdge { get; set; }
    [Column(Order = 8)]
    public long RewardStakedShared { get; set; }
    [Column(Order = 9)]
    public long BonusDelegated { get; set; }
    [Column(Order = 10)]
    public long BonusStakedOwn { get; set; }
    [Column(Order = 11)]
    public long BonusStakedEdge { get; set; }
    [Column(Order = 12)]
    public long BonusStakedShared { get; set; }

    [Column(nameof(BakerFees), Order = 13)]
    public long BakerFees { get; set; }

    [Column(nameof(BurnedFees), Order = 14)]
    public long BurnedFees { get; set; }

    [Column(nameof(GasUsed), Order = 17)]
    public int GasUsed { get; set; }

    [Column(nameof(ProposerId), Order = 28)]
    public int? ProposerId { get; set; }
    [Column(Order = 29)]
    public int? ProducerId { get; set; }
    [Column(Order = 15)]
    public long? RevelationId { get; set; }
    [Column(Order = 30)]
    public int? ResetBakerDeactivation { get; set; }
    [Column(Order = 31)]
    public int? ResetProposerDeactivation { get; set; }

    [Column(Order = 34)]
    public bool? LBToggle { get; set; }
    [Column(Order = 32)]
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
