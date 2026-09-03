using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class RefutationGame
    {
        public required int Id { get; set; }
        public required int ChainId { get; set; }
        public required int SmartRollupId { get; set; }
        public required int InitiatorId { get; set; }
        public required int OpponentId { get; set; }
        public required int InitiatorCommitmentId { get; set; }
        public required int OpponentCommitmentId { get; set; }
        public required long LastMoveId { get; set; }

        public required int FirstLevel { get; set; }
        public required int LastLevel { get; set; }

        public long? InitiatorReward { get; set; }
        public long? InitiatorLoss { get; set; }
        public long? OpponentReward { get; set; }
        public long? OpponentLoss { get; set; }
    }

    public static class RefutationGameModel
    {
        public static void BuildRefutationGameModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<RefutationGame>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<RefutationGame>()
                .HasIndex(x => x.SmartRollupId);

            modelBuilder.Entity<RefutationGame>()
                .HasIndex(x => x.InitiatorCommitmentId);

            modelBuilder.Entity<RefutationGame>()
                .HasIndex(x => x.OpponentCommitmentId);
            #endregion
        }
    }
}
