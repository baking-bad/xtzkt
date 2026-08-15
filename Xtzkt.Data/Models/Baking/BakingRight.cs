using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class BakingRight
    {
        #region settings
        public const int MaxRound = 7;
        #endregion

        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Cycle { get; set; }
        public required int Level { get; set; }
        public required int BakerId { get; set; }
        public required BakingRightType Type { get; set; }
        public BakingRightStatus Status { get; set; }
        public int? Round { get; set; }
        public int? Slots { get; set; }
    }

    public enum BakingRightType
    {
        Baking = 0,
        Attestation = 1
    }

    public enum BakingRightStatus
    {
        Future = 0,
        Realized = 1,
        Missed = 2
    }

    public static class BakingRightModel
    {
        public static void BuildBakingRightModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<BakingRight>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<BakingRight>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
