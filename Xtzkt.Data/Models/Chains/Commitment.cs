using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class Commitment
    {
        public required int ChainId { get; set; }
        public required string Hash { get; set; }

        public long Balance { get; set; }

        public int? AddressId { get; set; }
        public int? Level { get; set; }
    }

    public static class CommitmentModel
    {
        public static void BuildCommitmentModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<Commitment>()
                .HasKey(x => new { x.ChainId, x.Hash });
            #endregion

            #region props
            modelBuilder.Entity<Commitment>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(37)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<Commitment>()
                .HasIndex(x => x.AddressId);
            #endregion
        }
    }
}
