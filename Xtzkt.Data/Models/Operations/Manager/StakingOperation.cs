using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class StakingOperation : IManagerOperation
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

        public int StakerId { get; set; }
        public StakingAction Action { get; set; }
        public long RequestedAmount { get; set; }

        public long? Amount { get; set; }
        public int? BakerId { get; set; }
        public int? StakingUpdatesCount { get; set; }
    }

    public enum StakingAction
    {
        Stake,
        Unstake,
        Finalize
    }

    public static class StakingOperationModel
    {
        public static void BuildStakingOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<StakingOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<StakingOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<StakingOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
