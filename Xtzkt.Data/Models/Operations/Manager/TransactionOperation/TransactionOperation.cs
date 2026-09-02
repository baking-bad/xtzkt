using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;
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
        public required byte[] Hash { get; set; }

        public int SenderId { get; set; }
        public int Counter { get; set; }
        public int? GasLimit { get; set; } // null for internal operations
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

        #region binary writer
        private protected const string BinaryColumns = $"""
            "{nameof(Id)}",
            "{nameof(Direction)}",
            "{nameof(ChainId)}",
            "{nameof(Level)}",
            "{nameof(Timestamp)}",
            "{nameof(Hash)}",
            "{nameof(SenderId)}",
            "{nameof(Counter)}",
            "{nameof(GasLimit)}",
            "{nameof(GasUsed)}",
            "{nameof(Status)}",
            "{nameof(Errors)}",
            "{nameof(InitiatorId)}",
            "{nameof(TokenTransfers)}",
            "{nameof(SenderCodeHash)}",
            "{nameof(TargetId)}",
            "{nameof(TargetCodeHash)}",
            "{nameof(InternalOperations)}",
            "{nameof(LogsCount)}",
            "{nameof(Entrypoint)}",
            "{nameof(Parameters)}",
            "{nameof(Guessed)}",
            "{nameof(SubsCounter)}"
            """;

        private protected void WriteBinaryBase(NpgsqlBinaryImporter writer)
        {
            writer.StartRow();

            writer.Write(Id, NpgsqlDbType.Bigint);
            writer.Write((int)Direction, NpgsqlDbType.Integer);
            writer.Write(ChainId, NpgsqlDbType.Integer);
            writer.Write(Level, NpgsqlDbType.Integer);
            writer.Write(Timestamp, NpgsqlDbType.TimestampTz);
            writer.Write(Hash, NpgsqlDbType.Bytea);
            writer.Write(SenderId, NpgsqlDbType.Integer);
            writer.Write(Counter, NpgsqlDbType.Integer);
            writer.WriteNullable(GasLimit, NpgsqlDbType.Integer);
            writer.Write(GasUsed, NpgsqlDbType.Integer);
            writer.Write((int)Status, NpgsqlDbType.Smallint);
            writer.WriteNullable(Errors, NpgsqlDbType.Text);
            writer.WriteNullable(InitiatorId, NpgsqlDbType.Integer);
            writer.WriteNullable(TokenTransfers, NpgsqlDbType.Integer);
            writer.WriteNullable(SenderCodeHash, NpgsqlDbType.Integer);
            writer.Write(TargetId, NpgsqlDbType.Integer);
            writer.WriteNullable(TargetCodeHash, NpgsqlDbType.Integer);
            writer.WriteNullable(InternalOperations, NpgsqlDbType.Integer);
            writer.WriteNullable(LogsCount, NpgsqlDbType.Integer);
            writer.WriteNullable(Entrypoint, NpgsqlDbType.Text);
            writer.WriteNullable(Parameters, NpgsqlDbType.Jsonb);
            writer.WriteNullable(Guessed, NpgsqlDbType.Boolean);
            writer.WriteNullable(SubsCounter, NpgsqlDbType.Integer);
        }
        #endregion
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
