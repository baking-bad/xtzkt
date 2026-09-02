using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class DelegationOperation : IInternalOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }
        public int SenderId { get; set; }
        public int Counter { get; set; }
        public long? BakerFee { get; set; } // null for internal operations
        public long? StorageFee { get; set; }
        public long? AllocationFee { get; set; }
        public int? GasLimit { get; set; } // null for internal operations
        public int GasUsed { get; set; }
        public int? StorageLimit { get; set; } // null for internal operations
        public int StorageUsed { get; set; }
        public OperationStatus Status { get; set; }
        public string? Errors { get; set; }
        public int? InitiatorId { get; set; }
        public int? Nonce { get; set; }

        public int? SenderCodeHash { get; set; }
        public int? BakerId { get; set; }
        public int? PrevBakerId { get; set; }
        public int? PrevDelegationLevel { get; set; }
        public DateTime? PrevDelegationTimestamp { get; set; }
        public int? PrevDeactivationLevel { get; set; }

        public long Amount { get; set; }

        public int? StakingUpdatesCount { get; set; }
    }

    public static class DelegationOperationModel
    {
        public static void BuildDelegationOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<DelegationOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<DelegationOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<DelegationOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<DelegationOperation>()
                .HasIndex(x => x.SenderId, $"IX_{nameof(XtzktContext.DelegationOps)}_{nameof(DelegationOperation.SenderId)}_Partial")
                .HasFilter($@"""{nameof(DelegationOperation.Status)}"" = {(int)OperationStatus.Applied}");
            #endregion
        }
    }
}
