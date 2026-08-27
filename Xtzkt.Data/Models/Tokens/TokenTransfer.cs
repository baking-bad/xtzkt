using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Xtzkt.Data.Models
{
    public class TokenTransfer
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required long TokenId { get; set; }
        public required int ContractId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public BigInteger Amount { get; set; }

        public int? FromId { get; set; }
        public byte[]? FromEntrypoint { get; set; }
        public int? ToId { get; set; }
        public byte[]? ToEntrypoint { get; set; }

        public long? OriginationId { get; set; }
        public long? TransactionId { get; set; }
        public long? MigrationId { get; set; }
        public int? IndexedAt { get; set; }

        #region binary writer
        public static void Write(NpgsqlConnection conn, IEnumerable<TokenTransfer> transfers)
        {
            using var writer = conn.BeginBinaryImport($"""
                COPY "{nameof(XtzktContext.TokenTransfers)}" (
                    "{nameof(Id)}",
                    "{nameof(ChainId)}",
                    "{nameof(TokenId)}",
                    "{nameof(ContractId)}",
                    "{nameof(Level)}",
                    "{nameof(Timestamp)}",
                    "{nameof(Amount)}",
                    "{nameof(FromId)}",
                    "{nameof(FromEntrypoint)}",
                    "{nameof(ToId)}",
                    "{nameof(ToEntrypoint)}",
                    "{nameof(OriginationId)}",
                    "{nameof(TransactionId)}",
                    "{nameof(MigrationId)}",
                    "{nameof(IndexedAt)}"
                )
                FROM STDIN (FORMAT BINARY)
                """);

            foreach (var transfer in transfers)
            {
                writer.StartRow();

                writer.Write(transfer.Id, NpgsqlDbType.Bigint);
                writer.Write(transfer.ChainId, NpgsqlDbType.Integer);
                writer.Write(transfer.TokenId, NpgsqlDbType.Bigint);
                writer.Write(transfer.ContractId, NpgsqlDbType.Integer);
                writer.Write(transfer.Level, NpgsqlDbType.Integer);
                writer.Write(transfer.Timestamp, NpgsqlDbType.TimestampTz);
                writer.Write(transfer.Amount, NpgsqlDbType.Numeric);
                writer.WriteNullable(transfer.FromId, NpgsqlDbType.Integer);
                writer.WriteNullable(transfer.FromEntrypoint, NpgsqlDbType.Bytea);
                writer.WriteNullable(transfer.ToId, NpgsqlDbType.Integer);
                writer.WriteNullable(transfer.ToEntrypoint, NpgsqlDbType.Bytea);
                writer.WriteNullable(transfer.OriginationId, NpgsqlDbType.Bigint);
                writer.WriteNullable(transfer.TransactionId, NpgsqlDbType.Bigint);
                writer.WriteNullable(transfer.MigrationId, NpgsqlDbType.Bigint);
                writer.WriteNullable(transfer.IndexedAt, NpgsqlDbType.Integer);
            }

            writer.Complete();
        }
        #endregion
    }

    public static class TokenTransferModel
    {
        public static void BuildTokenTransferModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<TokenTransfer>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<TokenTransfer>()
                .HasIndex(x => new { x.ChainId, x.Level });
            #endregion
        }
    }
}
