using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class SnapshotBalance
    {
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required int BakerId { get; set; }
        public required int AddressId { get; set; }
        
        public int? DelegatorsCount { get; set; }
        public long OwnDelegatedBalance { get; set; }
        public long? ExternalDelegatedBalance { get; set; }

        public long? OwnStakedBalance { get; set; }
        public long? ExternalStakedBalance { get; set; }
        public int? StakersCount { get; set; }

        public BigInteger? Pseudotokens { get; set; }

        #region helpers
        [NotMapped]
        public long StakingBalance => OwnDelegatedBalance + (ExternalDelegatedBalance ?? 0) + (OwnStakedBalance ?? 0) + (ExternalStakedBalance ?? 0);
        #endregion
    }

    public static class SnapshotBalanceModel
    {
        public static void BuildSnapshotBalanceModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SnapshotBalance>()
                .HasKey(x => new { x.ChainId, x.Level, x.BakerId, x.AddressId });
            #endregion

            #region indexes
            modelBuilder.Entity<SnapshotBalance>()
                .HasIndex(x => new { x.ChainId, x.Level }, $"IX_{nameof(XtzktContext.SnapshotBalances)}_{nameof(SnapshotBalance.ChainId)}_{nameof(SnapshotBalance.Level)}_Partial")
                .HasFilter(@"""BakerId"" = ""AddressId""");
            #endregion
        }
    }
}
