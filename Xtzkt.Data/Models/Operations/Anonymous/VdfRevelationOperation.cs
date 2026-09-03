using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class VdfRevelationOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }

        public required int BakerId { get; set; }
        public int Cycle { get; set; }
        public long RewardDelegated { get; set; }
        public long RewardStakedOwn { get; set; }
        public long RewardStakedEdge { get; set; }
        public long RewardStakedShared { get; set; }
        public required byte[] Solution { get; set; }
        public required byte[] Proof { get; set; }
    }

    public static class VdfRevelationOperationModel
    {
        public static void BuildVdfRevelationOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<VdfRevelationOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<VdfRevelationOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<VdfRevelationOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<VdfRevelationOperation>()
                //.HasIndex(x => new { x.ChainId, x.Cycle });
                .HasIndex(x => x.Cycle);
            #endregion
        }
    }
}
