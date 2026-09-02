using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public abstract class TransactionOperation(Direction direction) : IExplicitOperation, IParentOperation, ISourceOperation, ILogsOperation
    {
        [Column(Order = 36)]
        public Direction Direction { get; private set; } = direction;

        [Column(Order = 0)]
        public required long Id { get; set; }
        [Column(Order = 2)]
        public required int ChainId { get; set; }
        [Column(Order = 3)]
        public required int Level { get; set; }
        [Column(Order = 1)]
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }

        [Column(Order = 4)]
        public int SenderId { get; set; }
        [Column(Order = 5)]
        public int Counter { get; set; }
        [Column(Order = 21)]
        public int? GasLimit { get; set; } // null for internal operations
        [Column(Order = 6)]
        public int GasUsed { get; set; }
        [Column(Order = 37)]
        public OperationStatus Status { get; set; }
        public string? Errors { get; set; }
        [Column(Order = 17)]
        public int? InitiatorId { get; set; }
        [Column(Order = 27)]
        public int? TokenTransfers { get; set; }
        [Column(Order = 19)]
        public int? SenderCodeHash { get; set; }
        [Column(Order = 7)]
        public int TargetId { get; set; }
        [Column(Order = 20)]
        public int? TargetCodeHash { get; set; }
        [Column(Order = 24)]
        public int? InternalOperations { get; set; }
        [Column(Order = 25)]
        public int? LogsCount { get; set; }
        public string? Entrypoint { get; set; }
        public string? Parameters { get; set; }
        [Column(Order = 40)]
        public bool? Guessed { get; set; }

        [Column(Order = 26)]
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
            writer.Write((short)Direction, NpgsqlDbType.Smallint);
            writer.Write(ChainId, NpgsqlDbType.Integer);
            writer.Write(Level, NpgsqlDbType.Integer);
            writer.Write(Timestamp, NpgsqlDbType.TimestampTz);
            writer.Write(Hash, NpgsqlDbType.Bytea);
            writer.Write(SenderId, NpgsqlDbType.Integer);
            writer.Write(Counter, NpgsqlDbType.Integer);
            writer.WriteNullable(GasLimit, NpgsqlDbType.Integer);
            writer.Write(GasUsed, NpgsqlDbType.Integer);
            writer.Write((short)Status, NpgsqlDbType.Smallint);
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
