using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class DrainDelegateOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required string Hash { get; set; }

        public required int BakerId { get; set; }
        public required int TargetId { get; set; }

        public long Amount { get; set; }
        public long Fee { get; set; }
        public long AllocationFee { get; set; }
    }

    public static class DrainDelegateOperationModel
    {
        public static void BuildDrainDelegateOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<DrainDelegateOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<DrainDelegateOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<DrainDelegateOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
