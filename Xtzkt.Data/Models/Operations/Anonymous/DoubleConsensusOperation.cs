using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class DoubleConsensusOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required string Hash { get; set; }

        public DoubleConsensusKind Kind { get; set; }

        public int AccusedLevel { get; set; }
        public int SlashedLevel { get; set; }

        public required int AccuserId { get; set; }
        public required int OffenderId { get; set; }
        
        public long Reward { get; set; }
        public long LostStaked { get; set; }
        public long LostUnstaked { get; set; }
        public long LostExternalStaked { get; set; }
        public long LostExternalUnstaked { get; set; }

        public int? StakingUpdatesCount { get; set; }
    }

    public enum DoubleConsensusKind
    {
        DoubleAttestation,
        DoublePreattestation
    }

    public static class DoubleConsensusOperationModel
    {
        public static void BuildDoubleConsensusOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<DoubleConsensusOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<DoubleConsensusOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<DoubleConsensusOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<DoubleConsensusOperation>()
                //.HasIndex(x => new { x.ChainId, x.Hash });
                .HasIndex(x => x.Hash);

            modelBuilder.Entity<DoubleConsensusOperation>()
                //.HasIndex(x => new { x.ChainId, x.SlashedLevel });
                .HasIndex(x => x.SlashedLevel);
            #endregion
        }
    }
}
