using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public abstract class MichelsonTransactionOperation(Direction direction) : TransactionOperation(direction), IContractOperation
    {
        [Column(nameof(StorageFee), Order = 9)]
        public long? StorageFee { get; set; }

        [Column(nameof(AllocationFee), Order = 10)]
        public long? AllocationFee { get; set; }

        [Column(nameof(StorageLimit), Order = 22)]
        public int? StorageLimit { get; set; } // null for internal operations

        [Column(nameof(StorageUsed), Order = 23)]
        public int StorageUsed { get; set; }

        [Column(nameof(Nonce), Order = 18)]
        public int? Nonce { get; set; }


        [Column(nameof(Amount), Order = 8)]
        public long Amount { get; set; }


        [Column(nameof(StorageId), Order = 11)]
        public long? StorageId { get; set; }

        [Column(nameof(BigMapUpdates), Order = 29)]
        public int? BigMapUpdates { get; set; }

        [Column(nameof(TicketTransfers), Order = 28)]
        public int? TicketTransfers { get; set; }

        [Column(nameof(AddressRegistryIndex), Order = 32)]
        public int? AddressRegistryIndex { get; set; }

        [Column(nameof(ParametersRaw))]
        public byte[]? ParametersRaw { get; set; }
    }

    public static class MichelsonTransactionOperationModel
    {
        public static void BuildMichelsonTransactionOperationModel(this ModelBuilder modelBuilder)
        {
            #region indexes
            modelBuilder.Entity<MichelsonTransactionOperation>()
                .HasIndex(x => x.TargetId, $"IX_{nameof(XtzktContext.TransactionOps)}_{nameof(MichelsonTransactionOperation.TargetId)}_Partial")
                .HasFilter($@"""{nameof(MichelsonTransactionOperation.Entrypoint)}"" = 'transfer' AND ""{nameof(MichelsonTransactionOperation.TokenTransfers)}"" IS NULL AND ""{nameof(MichelsonTransactionOperation.Status)}"" = {(int)OperationStatus.Applied}");
            #endregion
        }
    }
}
