using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class DelegationSnapshot
    {
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required int BakerId { get; set; }
        public required int AddressId { get; set; }
        
        public long OwnDelegatedBalance { get; set; }
        public long? ExternalDelegatedBalance { get; set; }
        public int? DelegatorsCount { get; set; }

        public int? PrevMinTotalDelegatedLevel { get; set; }
        public long? PrevMinTotalDelegated {  get; set; }
    }

    public static class DelegationSnapshotModel
    {
        public static void BuildDelegationSnapshotModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<DelegationSnapshot>()
                .HasKey(x => new { x.ChainId, x.Level, x.BakerId, x.AddressId });
            #endregion

            #region indexes
            modelBuilder.Entity<DelegationSnapshot>()
                .HasIndex(x => new { x.ChainId, x.Level }, $"IX_{nameof(XtzktContext.DelegationSnapshots)}_{nameof(DelegationSnapshot.ChainId)}_{nameof(DelegationSnapshot.Level)}_Partial")
                .HasFilter(@"""BakerId"" = ""AddressId""");
            #endregion
        }
    }
}
