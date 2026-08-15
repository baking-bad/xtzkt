using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Numerics;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public abstract class IncreasePaidStorageOperation(Layer layer) : IManagerOperation
{
    public Layer Layer { get; private set; } = layer;

    public required long Id { get; set; }
    public required int ChainId { get; set; }
    public required int Level { get; set; }
    public required DateTime Timestamp { get; set; }
    public required string Hash { get; set; }
    public int SenderId { get; set; }
    public int Counter { get; set; }
    public long? StorageFee { get; set; }
    public int GasLimit { get; set; }
    public int GasUsed { get; set; }
    public int StorageLimit { get; set; }
    public int StorageUsed { get; set; }
    public OperationStatus Status { get; set; }
    public string? Errors { get; set; }

    public int ContractId { get; set; }
    public BigInteger Amount { get; set; }
}

public static class IncreasePaidStorageOperationModel
{
    public static void BuildIncreasePaidStorageOperationModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<IncreasePaidStorageOperation>()
            .HasKey(x => x.Id);
        #endregion

        #region props
        modelBuilder.Entity<IncreasePaidStorageOperation>()
            .Property(x => x.Hash)
            .IsFixedLength(true)
            .HasMaxLength(51)
            .IsRequired();
        #endregion

        #region indexes
        modelBuilder.Entity<IncreasePaidStorageOperation>()
            //.HasIndex(x => new { x.ChainId, x.Level });
            .HasIndex(x => x.Level);
        #endregion

        #region inheritance
        modelBuilder.Entity<IncreasePaidStorageOperation>()
            .HasDiscriminator<Layer>(nameof(IncreasePaidStorageOperation.Layer))
            .HasValue<L1IncreasePaidStorageOperation>(Layer.L1)
            .HasValue<XIncreasePaidStorageOperation>(Layer.TezosX);

        modelBuilder.Entity<IncreasePaidStorageOperation>()
            .Property(x => x.Layer)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildL1IncreasePaidStorageOperationModel();
        modelBuilder.BuildXIncreasePaidStorageOperationModel();
        #endregion
    }
}
