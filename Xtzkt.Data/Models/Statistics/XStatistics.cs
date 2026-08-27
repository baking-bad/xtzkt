using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Xtzkt.Data.Models
{
    public class XStatistics() : Statistics(Layer.TezosX)
    {
        const string TotalBootstrappedColumn = $"{nameof(XStatistics)}_{nameof(TotalBootstrapped)}";
        const string TotalCreatedColumn = $"{nameof(XStatistics)}_{nameof(TotalCreated)}";
        const string TotalBurnedColumn = $"{nameof(XStatistics)}_{nameof(TotalBurned)}";
        const string TotalBanishedColumn = $"{nameof(XStatistics)}_{nameof(TotalBanished)}";
        const string TotalLostColumn = $"{nameof(XStatistics)}_{nameof(TotalLost)}";

        #region supply
        [Column(TotalBootstrappedColumn)]
        public BigInteger TotalBootstrapped { get; set; }

        [Column(TotalCreatedColumn)]
        public BigInteger TotalCreated { get; set; }

        [Column(TotalBurnedColumn)]
        public BigInteger TotalBurned { get; set; }

        [Column(TotalBanishedColumn)]
        public BigInteger TotalBanished { get; set; }

        [Column(TotalLostColumn)]
        public BigInteger TotalLost { get; set; }
        #endregion

        #region binary writer
        public static void Write(NpgsqlConnection conn, IEnumerable<XStatistics> statistics)
        {
            using var writer = conn.BeginBinaryImport($"""
                COPY "{nameof(XtzktContext.Statistics)}" (
                    {BinaryColumns},
                    "{TotalBootstrappedColumn}",
                    "{TotalCreatedColumn}",
                    "{TotalBurnedColumn}",
                    "{TotalBanishedColumn}",
                    "{TotalLostColumn}"
                )
                FROM STDIN (FORMAT BINARY)
                """);

            foreach (var stats in statistics)
            {
                stats.WriteBinaryBase(writer);

                writer.Write(stats.TotalBootstrapped, NpgsqlDbType.Numeric);
                writer.Write(stats.TotalCreated, NpgsqlDbType.Numeric);
                writer.Write(stats.TotalBurned, NpgsqlDbType.Numeric);
                writer.Write(stats.TotalBanished, NpgsqlDbType.Numeric);
                writer.Write(stats.TotalLost, NpgsqlDbType.Numeric);
            }

            writer.Complete();
        }
        #endregion
    }

    public static class XStatisticsModel
    {
        public static void BuildXStatisticsModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
