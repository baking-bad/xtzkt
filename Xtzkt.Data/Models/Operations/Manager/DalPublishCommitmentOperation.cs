using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class DalPublishCommitmentOperation : IManagerOperation
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

        public int Slot { get; set; }
        public required string Commitment {  get; set; }
    }

    public static class DalPublishCommitmentOperationModel
    {
        public static void BuildDalPublishCommitmentOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<DalPublishCommitmentOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<DalPublishCommitmentOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<DalPublishCommitmentOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
