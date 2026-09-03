using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models
{
    public class ActivationOperation : IExplicitOperation
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }
        public required byte[] Hash { get; set; }

        public required int AddressId { get; set; }
        public long Balance { get; set; }
    }

    public static class ActivationOperationModel
    {
        public static void BuildActivationOperationModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<ActivationOperation>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<ActivationOperation>()
                .Property(x => x.Hash)
                .IsRequired();
            #endregion

            #region indexes
            modelBuilder.Entity<ActivationOperation>()
                //.HasIndex(x => new { x.ChainId, x.Level });
                .HasIndex(x => x.Level);
            #endregion
        }
    }
}
