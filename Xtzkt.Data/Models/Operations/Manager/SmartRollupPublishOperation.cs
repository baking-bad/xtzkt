using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SmartRollupPublishOperation : IManagerOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required string Hash { get; set; }
        public int SenderId { get; set; }
        public int Counter { get; set; }
        public long BakerFee { get; set; }
        public long? StorageFee { get; set; }
        public long? AllocationFee { get; set; }
        public int GasLimit { get; set; }
        public int GasUsed { get; set; }
        public int StorageLimit { get; set; }
        public int StorageUsed { get; set; }
        public OperationStatus Status { get; set; }
        public string? Errors { get; set; }

        public int? SmartRollupId { get; set; }
        public int? CommitmentId { get; set; }
        public long Bond { get; set; }
        public SmartRollupBondStatus? BondStatus { get; set; }
        public SmartRollupPublishFlags Flags { get; set; }
    }

    public enum SmartRollupBondStatus
    {
        Active,
        Returned,
        Lost
    }

    [Flags]
    public enum SmartRollupPublishFlags
    {
        None                = 0b_0000,
        AddStaker           = 0b_0001,
        ReactivateStaker    = 0b_0010,
        ReactivateBranch    = 0b_0100
    }

    public static class SmartRollupPublishOperationModel
    {
        public static void BuildSmartRollupPublishOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SmartRollupPublishOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<SmartRollupPublishOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<SmartRollupPublishOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<SmartRollupPublishOperation>()
                .HasIndex(x => x.CommitmentId);

            modelBuilder.Entity<SmartRollupPublishOperation>()
                .HasIndex(x => new { x.SmartRollupId, x.BondStatus, x.SenderId })
                .HasFilter($@"""{nameof(SmartRollupPublishOperation.BondStatus)}"" IS NOT NULL");

            modelBuilder.Entity<SmartRollupPublishOperation>()
                .HasIndex(x => new { x.SmartRollupId, x.SenderId, x.Id });
            #endregion
        }
    }
}
