using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public abstract class TransactionOperation(Direction direction) : IExplicitOperation, IParentOperation, ISourceOperation, ILogsOperation
    {
        public Direction Direction { get; private set; } = direction;

        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required string Hash { get; set; }

        public int SenderId { get; set; }
        public int Counter { get; set; }
        public int GasLimit { get; set; }
        public int GasUsed { get; set; }
        public OperationStatus Status { get; set; }
        public string? Errors { get; set; }
        public int? InitiatorId { get; set; }
        public int? TokenTransfers { get; set; }
        public int? SenderCodeHash { get; set; }
        public int TargetId { get; set; }
        public int? TargetCodeHash { get; set; }
        public int? InternalOperations { get; set; }
        public int? LogsCount { get; set; }
        public string? Entrypoint { get; set; }
        public string? Parameters { get; set; }
        public bool? Guessed { get; set; }

        public int? SubsCounter { get; set; }
    }

    public static class TransactionOperationModel
    {
        public static void BuildTransactionOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<TransactionOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<MichelsonTransactionOperation>()
                .Property(x => x.Parameters)
                .HasColumnType("jsonb");
            #endregion

            #region indexes
            modelBuilder.Entity<TransactionOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion

            #region inheritance
            modelBuilder.Entity<TransactionOperation>()
                .HasDiscriminator<Direction>(nameof(TransactionOperation.Direction))
                .HasValue<L1TransactionOperation>(Direction.L1)
                .HasValue<XEvmTransactionOperation>(Direction.XEvm)
                .HasValue<XMichelsonTransactionOperation>(Direction.XMichelson)
                .HasValue<XEvmMichelsonTransactionOperation>(Direction.XEvmMichelson)
                .HasValue<XMichelsonEvmTransactionOperation>(Direction.XMichelsonEvm);

            modelBuilder.Entity<TransactionOperation>()
                .Property(x => x.Direction)
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            modelBuilder.BuildMichelsonTransactionOperationModel();
            modelBuilder.BuildL1TransactionOperationModel();
            modelBuilder.BuildXEvmTransactionOperationModel();
            modelBuilder.BuildXMichelsonTransactionOperationModel();
            modelBuilder.BuildXEvmMichelsonTransactionOperationModel();
            modelBuilder.BuildXMichelsonEvmTransactionOperationModel();
            #endregion
        }
    }
}
