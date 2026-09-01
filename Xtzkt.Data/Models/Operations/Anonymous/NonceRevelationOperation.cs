using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class NonceRevelationOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }

        public required int BakerId { get; set; }
        public required int SenderId { get; set; }
        public int RevealedLevel { get; set; }
        public int RevealedCycle { get; set; }
        public long RewardDelegated { get; set; }
        public long RewardStakedOwn { get; set; }
        public long RewardStakedEdge { get; set; }
        public long RewardStakedShared { get; set; }
        public required byte[] Nonce { get; set; }
    }

    public static class NonceRevelationOperationModel
    {
        public static void BuildNonceRevelationOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<NonceRevelationOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<NonceRevelationOperation>()
                .Property(x => x.Hash)
                .IsRequired();

            modelBuilder.Entity<NonceRevelationOperation>()
                .Property(x => x.Nonce)
                .IsFixedLength(true)
                .HasMaxLength(32)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<NonceRevelationOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<NonceRevelationOperation>()
                //.HasIndex(x => new { x.ChainId, x.RevealedCycle });
                .HasIndex(x => x.RevealedCycle);
            #endregion
        }
    }
}
