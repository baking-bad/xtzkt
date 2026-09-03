using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public abstract class DepositOperation(Runtime runtime) : IOperation
{
    public Runtime Runtime { get; private set; } = runtime;

    public required long Id { get; set; }
    public required int ChainId { get; set; }
    public required int Level { get; set; }
    public required DateTime Timestamp { get; set; }
    public required byte[] Hash { get; set; }

    public OperationStatus Status { get; set; }
    public int GasUsed { get; set; }

    public required int InboxLevel { get; set; }
    public required int InboxMessageId { get; set; }
    public required int ReceiverId { get; set; }
    public required DepositType Type { get; set; }
}

public enum DepositType
{
    Xtz,
    Fa,
}

public static class DepositOperationModel
{
    public static void BuildDepositOperationModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<DepositOperation>()
            .HasKey(x => x.Id);
        #endregion

        #region indexes
        modelBuilder.Entity<DepositOperation>()
            //.HasIndex(x => new { x.ChainId, x.Level });
            .HasIndex(x => x.Level);
        #endregion

        #region inheritance
        modelBuilder.Entity<DepositOperation>()
            .HasDiscriminator<Runtime>(nameof(DepositOperation.Runtime))
            .HasValue<XMichelsonDepositOperation>(Runtime.Michelson)
            .HasValue<XEvmDepositOperation>(Runtime.Evm);

        modelBuilder.Entity<DepositOperation>()
            .Property(x => x.Runtime)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildXMichelsonDepositOperationModel();
        modelBuilder.BuildXEvmDepositOperationModel();
        #endregion
    }
}
