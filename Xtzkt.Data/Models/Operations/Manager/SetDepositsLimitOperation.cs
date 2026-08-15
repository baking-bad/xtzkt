using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SetDepositsLimitOperation : IManagerOperation
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

        public BigInteger? Limit { get; set; }
    }

    public static class SetDepositsLimitOperationModel
    {
        public static void BuildSetDepositsLimitOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SetDepositsLimitOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<SetDepositsLimitOperation>()
                .Property(x => x.Hash)
                .IsFixedLength(true)
                .HasMaxLength(51)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<SetDepositsLimitOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<SetDepositsLimitOperation>()
                .HasIndex(x => new { x.SenderId, x.Id });
            #endregion
        }
    }
}
