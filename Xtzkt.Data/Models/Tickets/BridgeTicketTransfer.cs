using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class BridgeTicketTransfer
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required long TicketId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public BigInteger Amount { get; set; }

        public int? FromId { get; set; }
        public int? ToId { get; set; }

        public long? TransactionId { get; set; }
        public long? DepositId { get; set; }
    }

    public static class BridgeTicketTransferModel
    {
        public static void BuildBridgeTicketTransferModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<BridgeTicketTransfer>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<BridgeTicketTransfer>()
                .HasIndex(x => new { x.ChainId, x.Level });
            #endregion
        }
    }
}
