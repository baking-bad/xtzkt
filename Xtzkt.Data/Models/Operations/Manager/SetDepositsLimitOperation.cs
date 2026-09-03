using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SetDepositsLimitOperation : IL1ManagerOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }
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

        #region IL1ManagerOperation
        int? IL1ManagerOperation.GasLimit { get => GasLimit; set => GasLimit = value ?? throw new InvalidOperationException($"{nameof(GasLimit)} cannot be null"); }
        long? IL1ManagerOperation.BakerFee { get => BakerFee; set => BakerFee = value ?? throw new InvalidOperationException($"{nameof(BakerFee)} cannot be null"); }
        #endregion
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
