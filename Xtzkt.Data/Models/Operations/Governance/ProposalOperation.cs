using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class ProposalOperation : IExplicitOperation
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
        public bool Duplicated { get; set; }
    }

    public static class ProposalOperationModel
    {
        public static void BuildProposalOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<ProposalOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<ProposalOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<ProposalOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<ProposalOperation>()
                .HasIndex(x => new { x.Period, x.ProposalId, x.SenderId });

            modelBuilder.Entity<ProposalOperation>()
                .HasIndex(x => new { x.SenderId, x.Id });
            #endregion
        }
    }
}
