using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class AttestationOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required string Hash { get; set; }

        public required int BakerId { get; set; }
        public long Power { get; set; }
        public long Reward { get; set; }
        public long Deposit { get; set; }

        public int? ResetDeactivation { get; set; }

        #region binary writer
        public static void Write(NpgsqlConnection conn, IEnumerable<AttestationOperation> ops)
        {
            using var writer = conn.BeginBinaryImport($"""
                COPY "{nameof(XtzktContext.AttestationOps)}" (
                    "{nameof(Id)}",
                    "{nameof(ChainId)}",
                    "{nameof(BakerId)}",
                    "{nameof(Power)}",
                    "{nameof(Reward)}",
                    "{nameof(Deposit)}",
                    "{nameof(ResetDeactivation)}",
                    "{nameof(Level)}",
                    "{nameof(Timestamp)}",
                    "{nameof(Hash)}"
                )
                FROM STDIN (FORMAT BINARY)
                """);

            foreach (var op in ops)
            {
                writer.StartRow();

                writer.Write(op.Id, NpgsqlDbType.Bigint);
                writer.Write(op.ChainId, NpgsqlDbType.Integer);
                writer.Write(op.BakerId, NpgsqlDbType.Integer);
                writer.Write(op.Power, NpgsqlDbType.Bigint);
                writer.Write(op.Reward, NpgsqlDbType.Bigint);
                writer.Write(op.Deposit, NpgsqlDbType.Bigint);
                writer.WriteNullable(op.ResetDeactivation, NpgsqlDbType.Integer);
                writer.Write(op.Level, NpgsqlDbType.Integer);
                writer.Write(op.Timestamp, NpgsqlDbType.TimestampTz);
                writer.Write(op.Hash, NpgsqlDbType.Char);
            }

            writer.Complete();
        }
        #endregion
    }

    public static class AttestationOperationModel
    {
        public static void BuildAttestationOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<AttestationOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<AttestationOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<AttestationOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
