using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SetDelegateParametersOperation : IManagerOperation
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

        public long? LimitOfStakingOverBaking { get; set; }
        public long? EdgeOfBakingOverStaking { get; set; }
        public int? ActivationCycle { get; set; }
    }

    public static class SetDelegateParametersOperationModel
    {
        public static void BuildSetDelegateParametersOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SetDelegateParametersOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<SetDelegateParametersOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<SetDelegateParametersOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<SetDelegateParametersOperation>()
                //.HasIndex(x => new { x.ChainId, x.ActivationCycle });
                .HasIndex(x => x.ActivationCycle, $"IX_{nameof(XtzktContext.SetDelegateParametersOps)}_{nameof(SetDelegateParametersOperation.ActivationCycle)}_Partial")
                .HasFilter($@"""{nameof(SetDelegateParametersOperation.Status)}"" = {(int)OperationStatus.Applied}");

            modelBuilder.Entity<SetDelegateParametersOperation>()
                .HasIndex(x => new { x.SenderId, x.Id });
            #endregion
        }
    }
}
