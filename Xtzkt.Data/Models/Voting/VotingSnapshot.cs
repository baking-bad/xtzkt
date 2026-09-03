using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class VotingSnapshot
    {
        public required int ChainId { get; set; }
        public required int Period { get; set; }
        public required int BakerId { get; set; }
        public required int Level { get; set; }
        public required long VotingPower { get; set; }

        public VoterStatus Status { get; set; }
    }

    public static class VotingSnapshotModel
    {
        public static void BuildVotingSnapshotModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<VotingSnapshot>()
                .HasKey(x => new { x.ChainId, x.Period, x.BakerId });
            #endregion
        }
    }

    public enum VoterStatus
    {
        None,
        Upvoted,
        VotedYay,
        VotedNay,
        VotedPass
    }
}
