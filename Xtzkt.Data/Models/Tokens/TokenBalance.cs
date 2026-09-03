using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class TokenBalance
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required long TokenId { get; set; }
        public required int ContractId { get; set; }
        public required int AddressId { get; set; }
        public required byte[]? Entrypoint { get; set; }
        public required int FirstLevel { get; set; }
        public required DateTime FirstTimestamp { get; set; }
        public required int LastLevel { get; set; }
        public required DateTime LastTimestamp { get; set; }
        public long TransfersCount { get; set; }
        public BigInteger Balance { get; set; }
        public int? IndexedAt { get; set; }
    }

    public static class TokenBalanceModel
    {
        public static void BuildTokenBalanceModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<TokenBalance>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<TokenBalance>()
                .HasIndex(x => new { x.AddressId, x.TokenId });

            modelBuilder.Entity<TokenBalance>()
                .HasIndex(x => x.TokenId);
            #endregion
        }
    }
}
