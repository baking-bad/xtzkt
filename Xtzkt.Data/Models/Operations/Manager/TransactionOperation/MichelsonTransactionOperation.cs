using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public abstract class MichelsonTransactionOperation(Direction direction) : TransactionOperation(direction), IContractOperation
    {
        [Column(nameof(StorageFee))]
        public long? StorageFee { get; set; }

        [Column(nameof(AllocationFee))]
        public long? AllocationFee { get; set; }

        [Column(nameof(StorageLimit))]
        public int StorageLimit { get; set; }

        [Column(nameof(StorageUsed))]
        public int StorageUsed { get; set; }

        [Column(nameof(Nonce))]
        public int? Nonce { get; set; }


        [Column(nameof(Amount))]
        public long Amount { get; set; }


        [Column(nameof(StorageId))]
        public long? StorageId { get; set; }

        [Column(nameof(BigMapUpdates))]
        public int? BigMapUpdates { get; set; }

        [Column(nameof(TicketTransfers))]
        public int? TicketTransfers { get; set; }

        [Column(nameof(AddressRegistryIndex))]
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
