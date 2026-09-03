using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class BallotOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }

        public required int Epoch { get; set; }
        public required int Period { get; set; }
        public required int ProposalId { get; set; }
        public required int SenderId { get; set; }

        public long VotingPower { get; set; }
        public Vote Vote { get; set; }
    }

    public static class BallotOperationModel
    {
        public static void BuildBallotOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<BallotOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<BallotOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<BallotOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<BallotOperation>()
                //.HasIndex(x => new { x.ChainId, x.Period });
                .HasIndex(x => x.Period);
            #endregion
        }
    }

    public enum Vote
    {
        Yay,
        Nay,
        Pass
    }
}
