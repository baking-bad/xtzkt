using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Numerics;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public abstract class TransferTicketOperation(Layer layer) : IManagerOperation, ISourceOperation, IParentOperation
    {
        public Layer Layer { get; private set; } = layer;

        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }
        public int SenderId { get; set; }
        public int Counter { get; set; }
        public long? StorageFee { get; set; }
        public int GasLimit { get; set; }
        public int GasUsed { get; set; }
        public int StorageLimit { get; set; }
        public int StorageUsed { get; set; }
        public OperationStatus Status { get; set; }
        public string? Errors { get; set; }

        public int TargetId { get; set; }
        public int TicketerId { get; set; }
        public BigInteger Amount { get; set; }

        public byte[]? RawType { get; set; }
        public byte[]? RawContent { get; set; }
        public string? JsonContent { get; set; }
        public required string Entrypoint { get; set; }

        public int? TicketTransfers { get; set; }
        public int? SubsCounter { get; set; }
        public int? InternalOperations { get; set; }
    }

    public static class TransferTicketOperationModel
    {
        public static void BuildTransferTicketOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<TransferTicketOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<TransferTicketOperation>()
                .Property(x => x.Hash)
                .IsRequired();

            modelBuilder.Entity<TransferTicketOperation>()
                .Property(x => x.JsonContent)
                .HasColumnType("jsonb");
            #endregion

            #region indexes
            modelBuilder.Entity<TransferTicketOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion

            #region inheritance
            modelBuilder.Entity<TransferTicketOperation>()
                .HasDiscriminator<Layer>(nameof(TransferTicketOperation.Layer))
                .HasValue<L1TransferTicketOperation>(Layer.L1)
                .HasValue<XTransferTicketOperation>(Layer.TezosX);

            modelBuilder.Entity<TransferTicketOperation>()
                .Property(x => x.Layer)
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            modelBuilder.BuildL1TransferTicketOperationModel();
            modelBuilder.BuildXTransferTicketOperationModel();
            #endregion
        }
    }
}
