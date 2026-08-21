using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class BridgeTicketBalance
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required long TicketId { get; set; }
        public required int AddressId { get; set; }
        public required int FirstLevel { get; set; }
        public required DateTime FirstTimestamp { get; set; }
        public required int LastLevel { get; set; }
        public required DateTime LastTimestamp { get; set; }
        public int TransfersCount { get; set; }
        public BigInteger Balance { get; set; }
    }

    public static class BridgeTicketBalanceModel
    {
        public static void BuildBridgeTicketBalanceModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<BridgeTicketBalance>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<BridgeTicketBalance>()
                .HasIndex(x => new { x.AddressId, x.TicketId });
            #endregion
        }
    }
}
