using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SmartRollupRecoverBondOperation : IManagerOperation
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
        public int? StakerId { get; set; }
        public long Bond { get; set; }
    }

    public static class SmartRollupRecoverBondOperationModel
    {
        public static void BuildSmartRollupRecoverBondOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SmartRollupRecoverBondOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<SmartRollupRecoverBondOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<SmartRollupRecoverBondOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
