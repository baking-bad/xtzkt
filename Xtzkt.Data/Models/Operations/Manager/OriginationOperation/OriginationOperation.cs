using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public abstract class OriginationOperation(Env env) : IExplicitOperation, ISourceOperation
{
    public Env Env { get; private set; } = env;

    public required long Id { get; set; }
    public required int ChainId { get; set; }
    public required int Level { get; set; }
    public required DateTime Timestamp { get; set; }
    public required byte[] Hash { get; set; }

    public int SenderId { get; set; }
    public int Counter { get; set; }
    public int GasLimit { get; set; }
    public int GasUsed { get; set; }
    public OperationStatus Status { get; set; }
    public string? Errors { get; set; }
    public int? InitiatorId { get; set; }
    public int? SenderCodeHash { get; set; }
    public int? ContractId { get; set; }
    public int? ContractCodeHash { get; set; }
    public int? ScriptId { get; set; }
    public int? TokenTransfers { get; set; }

    public int? SubsCounter { get; set; }
}

public static class OriginationOperationModel
{
    public static void BuildOriginationOperationModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<OriginationOperation>()
            .HasKey(x => x.Id);
        #endregion

        #region indexes
        modelBuilder.Entity<OriginationOperation>()
            //.HasIndex(x => new { x.ChainId, x.Level });
            .HasIndex(x => x.Level);
        #endregion

        #region inheritance
        modelBuilder.Entity<OriginationOperation>()
            .HasDiscriminator<Env>(nameof(OriginationOperation.Env))
            .HasValue<L1OriginationOperation>(Env.L1)
            .HasValue<XEvmOriginationOperation>(Env.XEvm)
            .HasValue<XMichelsonOriginationOperation>(Env.XMichelson);

        modelBuilder.Entity<OriginationOperation>()
            .Property(x => x.Env)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        modelBuilder.BuildMichelsonOriginationOperationModel();
        modelBuilder.BuildL1OriginationOperationModel();
        modelBuilder.BuildXEvmOriginationOperationModel();
        modelBuilder.BuildXMichelsonOriginationOperationModel();
        #endregion
    }
}
