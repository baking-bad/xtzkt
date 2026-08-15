using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public abstract class RevealOperation(Layer layer) : IManagerOperation
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
    public long? AllocationFee { get; set; }
    public int GasLimit { get; set; }
    public int GasUsed { get; set; }
    public int StorageLimit { get; set; }
    public int StorageUsed { get; set; }
    public OperationStatus Status { get; set; }
    public string? Errors { get; set; }
}

public static class RevealOperationModel
{
    public static void BuildRevealOperationModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<RevealOperation>()
            .HasKey(x => x.Id);
        #endregion
        
        #region props
        modelBuilder.Entity<RevealOperation>()
            .Property(x => x.Hash)
            .IsFixedLength(true)
            .HasMaxLength(51)
            .IsRequired();
        #endregion

        #region indexes
        modelBuilder.Entity<RevealOperation>()
            //.HasIndex(x => new { x.ChainId, x.Level });
            .HasIndex(x => x.Level);
        #endregion

        #region inheritance
        modelBuilder.Entity<RevealOperation>()
            .HasDiscriminator<Layer>(nameof(RevealOperation.Layer))
            .HasValue<L1RevealOperation>(Layer.L1)
            .HasValue<XRevealOperation>(Layer.TezosX);

        modelBuilder.Entity<RevealOperation>()
            .Property(x => x.Layer)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildL1RevealOperationModel();
        modelBuilder.BuildXRevealOperationModel();
        #endregion
    }
}
