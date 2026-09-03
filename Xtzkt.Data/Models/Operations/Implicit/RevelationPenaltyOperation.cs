using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class RevelationPenaltyOperation : IOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }

        public required int BakerId { get; set; }

        public int MissedLevel { get; set; }
        public long Loss { get; set; }
    }

    public static class RevelationPenaltyOperationModel
    {
        public static void BuildRevelationPenaltyOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<RevelationPenaltyOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<RevelationPenaltyOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
