using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public abstract class RegisterConstantOperation(Layer layer) : IManagerOperation
{
    public Layer Layer { get; private set; } = layer;

    public required long Id { get; set; }
    public required int ChainId { get; set; }
    public required int Level { get; set; }
    public required DateTime Timestamp { get; set; }
    public required byte[] Hash { get; set; }
    public int SenderId { get; set; }
    public int Counter { get; set; }
    public long? StorageFee { get; set; }
    public int GasLimit { get; set; }
    public int GasUsed { get; set; }
    public int StorageLimit { get; set; }
    public int StorageUsed { get; set; }
    public OperationStatus Status { get; set; }
    public string? Errors { get; set; }

    public string? Address { get; set; }
    public byte[]? Value { get; set; }
    public int? Refs { get; set; }
}

public static class RegisterConstantOperationModel
{
    public static void BuildRegisterConstantOperationModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<RegisterConstantOperation>()
            .HasKey(x => x.Id);
        #endregion

        #region props
        modelBuilder.Entity<RegisterConstantOperation>()
            .Property(x => x.Hash)
            .IsRequired();

        modelBuilder.Entity<RegisterConstantOperation>()
            .Property(x => x.Address)
            .HasMaxLength(54); // expr

        // shadow property
        modelBuilder.Entity<RegisterConstantOperation>()
            .Property<string>("Extras")
            .HasColumnType("jsonb");
        #endregion

        #region indexes
        modelBuilder.Entity<RegisterConstantOperation>()
            //.HasIndex(x => new { x.ChainId, x.Level });
            .HasIndex(x => x.Level);

        modelBuilder.Entity<RegisterConstantOperation>()
            //.HasIndex(x => new { x.ChainId, x.Address });
            .HasIndex(x => x.Address)
            .HasFilter($@"""{nameof(RegisterConstantOperation.Address)}"" IS NOT NULL");
        #endregion

        #region inheritance
        modelBuilder.Entity<RegisterConstantOperation>()
            .HasDiscriminator<Layer>(nameof(RegisterConstantOperation.Layer))
            .HasValue<L1RegisterConstantOperation>(Layer.L1)
            .HasValue<XRegisterConstantOperation>(Layer.TezosX);

        modelBuilder.Entity<RegisterConstantOperation>()
            .Property(x => x.Layer)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildL1RegisterConstantOperationModel();
        modelBuilder.BuildXRegisterConstantOperationModel();
        #endregion
    }
}
