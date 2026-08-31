using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Xtzkt.Data.Models;

public abstract class Chain(Layer layer)
{
    public Layer Layer { get; private set; } = layer;

    public required int Id { get; set; }
    public required string ChainId { get; set; }
    public required string Network { get; set; }

    public int Level { get; set; }
    public DateTime Timestamp { get; set; }
    public required string Hash { get; set; }

    public int KnownLevel { get; set; }
    public DateTime SyncedAt { get; set; }

    #region counters
    public int AddressCounter { get; set; }
    public long OperationCounter { get; set; }
    public int BigMapCounter { get; set; }
    public long BigMapKeyCounter { get; set; }
    public long BigMapUpdateCounter { get; set; }
    public long StorageCounter { get; set; }
    public int ScriptCounter { get; set; }
    public long LogsCounter { get; set; }
    #endregion

    #region counts
    public int ProtocolsCount { get; set; }
    public int BlocksCount { get; set; }
    public long RevealOpsCount { get; set; }
    public long TransactionOpsCount { get; set; }
    public long OriginationOpsCount { get; set; }
    public long RegisterConstantOpsCount { get; set; }
    public long IncreasePaidStorageOpsCount { get; set; }
    public long TransferTicketOpsCount { get; set; }
    public long MigrationOpsCount { get; set; }

    public int TokensCount { get; set; }
    public int TokenBalancesCount { get; set; }
    public long TokenTransfersCount { get; set; }

    public int TicketsCount { get; set; }
    public int TicketBalancesCount { get; set; }
    public int TicketTransfersCount { get; set; }

    public long LogsCount { get; set; }
    public int ConstantsCount { get; set; }
    #endregion
}

public static class ChainModel
{
    public static void BuildChainModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<Chain>()
            .HasKey(x => x.Id);
        #endregion

        #region props
        // manually assigned ids, not auto-increment
        modelBuilder.Entity<Chain>()
            .Property(x => x.Id)
            .ValueGeneratedNever();

        // shadow property
        modelBuilder.Entity<Chain>()
            .Property<string>("Extras")
            .HasColumnType("jsonb");
        #endregion

        #region inheritance
        modelBuilder.Entity<Chain>()
            .HasDiscriminator<Layer>(nameof(Chain.Layer))
            .HasValue<L1Chain>(Layer.L1)
            .HasValue<XChain>(Layer.TezosX);

        modelBuilder.Entity<Chain>()
            .Property(x => x.Layer)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildL1ChainModel();
        modelBuilder.BuildXChainModel();
        #endregion
    }
}
