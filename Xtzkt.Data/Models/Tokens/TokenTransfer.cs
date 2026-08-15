using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class TokenTransfer
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required long TokenId { get; set; }
        public required int ContractId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public BigInteger Amount { get; set; }

        public int? FromId { get; set; }
        public byte[]? FromEntrypoint { get; set; }
        public int? ToId { get; set; }
        public byte[]? ToEntrypoint { get; set; }

        public long? OriginationId { get; set; }
        public long? TransactionId { get; set; }
        public long? MigrationId { get; set; }
        public int? IndexedAt { get; set; }
    }

    public static class TokenTransferModel
    {
        public static void BuildTokenTransferModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<TokenTransfer>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<TokenTransfer>()
                .HasIndex(x => new { x.ChainId, x.Level });
            #endregion
        }
    }
}
