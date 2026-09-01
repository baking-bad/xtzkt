using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class SmartRollupRefuteOperation : IManagerOperation
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

        public int? SmartRollupId { get; set; }
        public int? GameId { get; set; }
        public RefutationMove Move { get; set; }
        public RefutationGameStatus GameStatus { get; set; }
        public long? DissectionStart { get; set; }
        public long? DissectionEnd { get; set; }
        public int? DissectionSteps { get; set; }
    }

    public enum RefutationMove
    {
        Start,
        Dissection,
        Proof,
        Timeout
    }

    public enum RefutationGameStatus
    {
        None,
        Ongoing,
        Loser,
        Draw
    }

    public static class SmartRollupRefuteOperationModel
    {
        public static void BuildSmartRollupRefuteOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<SmartRollupRefuteOperation>()
                .HasKey(x => x.Id);
            #endregion
            
            #region props
            modelBuilder.Entity<SmartRollupRefuteOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<SmartRollupRefuteOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);

            modelBuilder.Entity<SmartRollupRefuteOperation>()
                .HasIndex(x => new { x.GameId, x.Id });
            #endregion
        }
    }
}
