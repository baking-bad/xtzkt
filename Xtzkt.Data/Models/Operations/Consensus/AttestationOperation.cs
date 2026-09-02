using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class AttestationOperation : IExplicitOperation
    {
        [Column(Order = 0)]
        public required long Id { get; set; }
        [Column(Order = 5)]
        public required int ChainId { get; set; }
        [Column(Order = 6)]
        public required int Level { get; set; }
        [Column(Order = 1)]
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }

        [Column(Order = 7)]
        public required int BakerId { get; set; }
        [Column(Order = 2)]
        public long Power { get; set; }
        [Column(Order = 3)]
        public long? Reward { get; set; } // null since proto 12
        [Column(Order = 4)]
        public long? Deposit { get; set; } // null since proto 12

        [Column(Order = 8)]
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
                writer.WriteNullable(op.Reward, NpgsqlDbType.Bigint);
                writer.WriteNullable(op.Deposit, NpgsqlDbType.Bigint);
                writer.WriteNullable(op.ResetDeactivation, NpgsqlDbType.Integer);
                writer.Write(op.Level, NpgsqlDbType.Integer);
                writer.Write(op.Timestamp, NpgsqlDbType.TimestampTz);
                writer.Write(op.Hash, NpgsqlDbType.Bytea);
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
