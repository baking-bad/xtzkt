using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Xtzkt.Data.Models
{
    public class XStatistics() : Statistics(Layer.TezosX)
    {
        #region supply
        [Column($"{nameof(TotalBootstrapped)}18")]
        public BigInteger TotalBootstrapped { get; set; }

        [Column($"{nameof(TotalCreated)}18")]
        public BigInteger TotalCreated { get; set; }

        [Column($"{nameof(TotalBurned)}18")]
        public BigInteger TotalBurned { get; set; }

        [Column($"{nameof(TotalBanished)}18")]
        public BigInteger TotalBanished { get; set; }

        [Column($"{nameof(TotalLost)}18")]
        public BigInteger TotalLost { get; set; }
        #endregion

        #region binary writer
        public static void Write(NpgsqlConnection conn, IEnumerable<XStatistics> statistics)
        {
            using var writer = conn.BeginBinaryImport($"""
                COPY "{nameof(XtzktContext.Statistics)}" (
                    {BinaryColumns},
                    "{nameof(TotalBootstrapped)}18",
                    "{nameof(TotalCreated)}18",
                    "{nameof(TotalBurned)}18",
                    "{nameof(TotalBanished)}18",
                    "{nameof(TotalLost)}18"
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
