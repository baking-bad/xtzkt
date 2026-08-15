using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SmartRollupExecuteOperation : IManagerOperation, ISourceOperation, IParentOperation
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

        public int? TicketTransfers { get; set; }
        public int? SubsCounter { get; set; }
        public int? InternalOperations { get; set; }
    }

    public static class SmartRollupExecuteOperationModel
    {
        public static void BuildSmartRollupExecuteOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SmartRollupExecuteOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<SmartRollupExecuteOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<SmartRollupExecuteOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<SmartRollupExecuteOperation>()
                .HasIndex(x => new { x.CommitmentId, x.Id });
            #endregion
        }
    }
}
