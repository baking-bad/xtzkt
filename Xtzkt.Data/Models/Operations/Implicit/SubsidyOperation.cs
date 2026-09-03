using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SubsidyOperation : IOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }

        public required int AddressId { get; set; }
        public long Amount { get; set; }
        public long? StorageId { get; set; }
    }

    public static class SubsidyOperationModel
    {
        public static void BuildSubsidyOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SubsidyOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<SubsidyOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
