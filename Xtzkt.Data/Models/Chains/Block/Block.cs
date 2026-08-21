using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public abstract class Block(Layer layer) : ISourceOperation
{
    public Layer Layer { get; private set; } = layer;

    public required long Id { get; set; }
    public required int ChainId { get; set; }
    public required int Level { get; set; }
    public required string Hash { get; set; }
    public required DateTime Timestamp { get; set; }
    public required int ProtocolId { get; set; }

    public int? OpsCounter { get; set; }
    public int? SubsCounter { get; set; }
}

[Flags]
public enum AllBlockEvents
{
    None                    = 0b_0000_0000_0000_0000,

    CycleBegin              = 0b_0000_0000_0000_0001,
    CycleEnd                = 0b_0000_0000_0000_0010,
    ProtocolBegin           = 0b_0000_0000_0000_0100,
    ProtocolEnd             = 0b_0000_0000_0000_1000,

    Deactivations           = 0b_0000_0000_0001_0000,
    BalanceSnapshot         = 0b_0000_0000_0100_0000,
    DelegationSnapshot      = 0b_0010_0000_0000_0000,
    DoubleBakingSlashing    = 0b_0000_1000_0000_0000,
    DoubleConsensusSlashing = 0b_0001_0000_0000_0000,

    NewAddresses             = 0b_0000_0000_0010_0000,
    Bigmaps                 = 0b_0000_0000_1000_0000,
    Tokens                  = 0b_0000_0001_0000_0000,
    Events                  = 0b_0000_0010_0000_0000,
    Tickets                 = 0b_0000_0100_0000_0000,
    BridgeTickets           = 0b_0100_0000_0000_0000,
}

[Flags]
public enum AllOperations : long
{
    None                    = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000,

    Activation              = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001,
    DalEntrapmentEvidence   = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0000_0010,
    DoubleBaking            = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0000_0100,
    DoubleConsensus         = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0000_1000,
    DrainDelegate           = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0001_0000,
    NonceRevelation         = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0010_0000,
    VdfRevelation           = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0100_0000,

    Attestation             = 0b_0000_0000_0000_0000_0000_0000_0000_0000_1000_0000,
    Preattestation          = 0b_0000_0000_0000_0000_0000_0000_0000_0001_0000_0000,

    Ballot                  = 0b_0000_0000_0000_0000_0000_0000_0000_0010_0000_0000,
    Proposal                = 0b_0000_0000_0000_0000_0000_0000_0000_0100_0000_0000,

    Migration               = 0b_0000_0000_0000_0000_0000_0000_0000_1000_0000_0000,
    AttestationRewards      = 0b_0000_0000_0000_0000_0000_0000_0001_0000_0000_0000,
    Autostaking             = 0b_0000_0000_0000_0000_0000_0000_0010_0000_0000_0000,
    DalAttestationReward    = 0b_0000_0000_0000_0000_0000_0000_0100_0000_0000_0000,
    RevelationPenalty       = 0b_0000_0000_0000_0000_0000_0000_1000_0000_0000_0000,
    Subsidy                 = 0b_0000_0000_0000_0000_0000_0001_0000_0000_0000_0000,

    Deposit                 = 0b_0000_0000_0000_0000_0000_0010_0000_0000_0000_0000,
    Origination             = 0b_0000_0000_0000_0000_0000_0100_0000_0000_0000_0000,
    Transaction             = 0b_0000_0000_0000_0000_0000_1000_0000_0000_0000_0000,
    DalPublishCommitment    = 0b_0000_0000_0000_0000_0001_0000_0000_0000_0000_0000,
    Delegation              = 0b_0000_0000_0000_0000_0010_0000_0000_0000_0000_0000,
    IncreasePaidStorage     = 0b_0000_0000_0000_0000_0100_0000_0000_0000_0000_0000,
    RegisterConstant        = 0b_0000_0000_0000_0000_1000_0000_0000_0000_0000_0000,
    Reveal                  = 0b_0000_0000_0000_0001_0000_0000_0000_0000_0000_0000,
    SetDepositsLimits       = 0b_0000_0000_0000_0010_0000_0000_0000_0000_0000_0000,
    SetDelegateParameters   = 0b_0000_0000_0000_0100_0000_0000_0000_0000_0000_0000,
    SmartRollupAddMessages  = 0b_0000_0000_0000_1000_0000_0000_0000_0000_0000_0000,
    SmartRollupCement       = 0b_0000_0000_0001_0000_0000_0000_0000_0000_0000_0000,
    SmartRollupExecute      = 0b_0000_0000_0010_0000_0000_0000_0000_0000_0000_0000,
    SmartRollupOriginate    = 0b_0000_0000_0100_0000_0000_0000_0000_0000_0000_0000,
    SmartRollupPublish      = 0b_0000_0000_1000_0000_0000_0000_0000_0000_0000_0000,
    SmartRollupRecoverBond  = 0b_0000_0001_0000_0000_0000_0000_0000_0000_0000_0000,
    SmartRollupRefute       = 0b_0000_0010_0000_0000_0000_0000_0000_0000_0000_0000,
    Staking                 = 0b_0000_0100_0000_0000_0000_0000_0000_0000_0000_0000,
    TransferTicket          = 0b_0000_1000_0000_0000_0000_0000_0000_0000_0000_0000,
    UpdateSecondaryKey      = 0b_0001_0000_0000_0000_0000_0000_0000_0000_0000_0000,
}

public static class BlockModel
{
    public static void BuildBlockModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<Block>()
            .HasKey(x => x.Id);
        #endregion

        #region props
        // shadow property
        modelBuilder.Entity<Block>()
            .Property<string>("Extras")
            .HasColumnType("jsonb");
        #endregion

        #region inheritance
        modelBuilder.Entity<Block>()
            .HasDiscriminator<Layer>(nameof(Block.Layer))
            .HasValue<L1Block>(Layer.L1)
            .HasValue<XBlock>(Layer.TezosX);

        modelBuilder.Entity<Block>()
            .Property(x => x.Layer)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildL1BlockModel();
        modelBuilder.BuildXBlockModel();
        #endregion

        #region indexes
        modelBuilder.Entity<Block>()
            .HasIndex(x => new { x.ChainId, x.Level });
        #endregion
    }
}
