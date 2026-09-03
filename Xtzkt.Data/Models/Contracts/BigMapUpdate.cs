using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Xtzkt.Data.Models
{
    public class BigMapUpdate
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int BigMapId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required BigMapAction Action { get; set; }

        public long? OriginationId { get; set; }
        public long? TransactionId { get; set; }
        public long? MigrationId { get; set; }

        public long? BigMapKeyId { get; set; }
        public byte[]? RawValue { get; set; }
        public string? JsonValue { get; set; }

        #region binary writer
        public static void Write(NpgsqlConnection conn, IEnumerable<BigMapUpdate> updates)
        {
            using var writer = conn.BeginBinaryImport($"""
                COPY "{nameof(XtzktContext.BigMapUpdates)}" (
                    "{nameof(Id)}",
                    "{nameof(ChainId)}",
                    "{nameof(BigMapId)}",
                    "{nameof(Level)}",
                    "{nameof(Timestamp)}",
                    "{nameof(Action)}",
                    "{nameof(OriginationId)}",
                    "{nameof(TransactionId)}",
                    "{nameof(MigrationId)}",
                    "{nameof(BigMapKeyId)}",
                    "{nameof(RawValue)}",
                    "{nameof(JsonValue)}"
                )
                FROM STDIN (FORMAT BINARY)
                """);

            foreach (var update in updates)
            {
                writer.StartRow();

                writer.Write(update.Id, NpgsqlDbType.Bigint);
                writer.Write(update.ChainId, NpgsqlDbType.Integer);
                writer.Write(update.BigMapId, NpgsqlDbType.Integer);
                writer.Write(update.Level, NpgsqlDbType.Integer);
                writer.Write(update.Timestamp, NpgsqlDbType.TimestampTz);
                writer.Write((int)update.Action, NpgsqlDbType.Integer);
                writer.WriteNullable(update.OriginationId, NpgsqlDbType.Bigint);
                writer.WriteNullable(update.TransactionId, NpgsqlDbType.Bigint);
                writer.WriteNullable(update.MigrationId, NpgsqlDbType.Bigint);
                writer.WriteNullable(update.BigMapKeyId, NpgsqlDbType.Bigint);
                writer.WriteNullable(update.RawValue, NpgsqlDbType.Bytea);
                writer.WriteNullable(update.JsonValue, NpgsqlDbType.Jsonb);
            }

            writer.Complete();
        }
        #endregion
    }

    public enum BigMapAction
    {
        Allocate,
        AddKey,
        UpdateKey,
        RemoveKey,
        Remove
    }

    public static class BigMapUpdateModel
    {
        public static void BuildBigMapUpdateModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<BigMapUpdate>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<BigMapUpdate>()
                .Property(x => x.JsonValue)
                .HasColumnType("jsonb");
            #endregion

            #region indexes
            modelBuilder.Entity<BigMapUpdate>()
                .HasIndex(x => new { x.ChainId, x.Level });

            modelBuilder.Entity<BigMapUpdate>()
                .HasIndex(x => new { x.BigMapId, x.Id });

            modelBuilder.Entity<BigMapUpdate>()
                .HasIndex(x => new { x.BigMapKeyId, x.Id })
                .HasFilter($@"""{nameof(BigMapUpdate.BigMapKeyId)}"" IS NOT NULL");
            #endregion
        }
    }
}
