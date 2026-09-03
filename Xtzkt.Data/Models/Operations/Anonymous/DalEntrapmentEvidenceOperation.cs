using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class DalEntrapmentEvidenceOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }

        public required int AccuserId { get; set; }
        public required int OffenderId { get; set; }

        public int TrapLevel { get; set; }
        public int TrapSlotIndex { get; set; }
    }

    public static class DalEntrapmentEvidenceOperationModel
    {
        public static void BuildDalEntrapmentEvidenceOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<DalEntrapmentEvidenceOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<DalEntrapmentEvidenceOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<DalEntrapmentEvidenceOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
