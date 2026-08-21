using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class BridgeTicket
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required byte[] WeakHash { get; set; } // keccak256(forged_ticketer || forged_content)

        public required int FirstLevel { get; set; }
        public required DateTime FirstTimestamp { get; set; }
        public required int LastLevel { get; set; }
        public required DateTime LastTimestamp { get; set; }

        public int TransfersCount { get; set; }
        public int BalancesCount { get; set; }
        public int HoldersCount { get; set; }

        public BigInteger TotalMinted { get; set; }
        public BigInteger TotalBurned { get; set; }
        public BigInteger TotalSupply { get; set; }
    }

    public static class BridgeTicketModel
    {
        public static void BuildBridgeTicketModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<BridgeTicket>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<BridgeTicket>()
                .HasIndex(x => x.WeakHash);
            #endregion
        }
    }
}
