using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;

namespace Xtzkt.Data.Models
{
    public abstract class Statistics(Layer layer)
    {
        public Layer Layer { get; private set; } = layer;

        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public DateTime? Date { get; set; }

        #region binary writer
        private protected const string BinaryColumns = $"""
            "{nameof(ChainId)}",
            "{nameof(Level)}",
            "{nameof(Layer)}",
            "{nameof(Timestamp)}",
            "{nameof(Date)}"
            """;

        private protected void WriteBinaryBase(NpgsqlBinaryImporter writer)
        {
            writer.StartRow();

            writer.Write(ChainId, NpgsqlDbType.Integer);
            writer.Write(Level, NpgsqlDbType.Integer);
            writer.Write((short)Layer, NpgsqlDbType.Smallint);
            writer.Write(Timestamp, NpgsqlDbType.TimestampTz);
            writer.WriteNullable(Date, NpgsqlDbType.TimestampTz);
        }
        #endregion
    }

    public static class StatisticsModel
    {
        public static void BuildStatisticsModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<Statistics>()
                .HasKey(x => new { x.ChainId, x.Level });
            #endregion

            #region inheritance
            modelBuilder.Entity<Statistics>()
                .HasDiscriminator<Layer>(nameof(Statistics.Layer))
                .HasValue<L1Statistics>(Layer.L1)
                .HasValue<XStatistics>(Layer.TezosX);

            modelBuilder.Entity<Statistics>()
                .Property(x => x.Layer)
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            modelBuilder.BuildL1StatisticsModel();
            modelBuilder.BuildXStatisticsModel();
            #endregion
        }
    }
}
