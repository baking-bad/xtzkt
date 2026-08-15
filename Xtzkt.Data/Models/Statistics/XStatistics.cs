using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class XStatistics() : Statistics(Layer.TezosX)
    {
        #region supply
        public BigInteger TotalBootstrapped { get; set; }
        public BigInteger TotalCreated { get; set; }
        public BigInteger TotalBurned { get; set; }
        public BigInteger TotalBanished { get; set; }
        public BigInteger TotalLost { get; set; }
        #endregion
    }

    public static class XStatisticsModel
    {
        public static void BuildXStatisticsModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
