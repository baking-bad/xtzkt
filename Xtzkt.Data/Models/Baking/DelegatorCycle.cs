using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class DelegatorCycle
    {
        public required int Id { get; set; }
        public required int ChainId { get; set; }
        public required int Cycle { get; set; }
        public required int DelegatorId { get; set; }
        public required int BakerId { get; set; }

        public long DelegatedBalance { get; set; }
        public BigInteger? StakedPseudotokens { get; set; }
    }

    public static class DelegatorCycleModel
    {
        public static void BuildDelegatorCycleModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<DelegatorCycle>()
                .HasKey(x => x.Id);
            #endregion

            #region indexes
            modelBuilder.Entity<DelegatorCycle>()
                //.HasIndex(x => new { x.ChainId, x.Cycle });
                .HasIndex(x => x.Cycle);
            #endregion
        }
    }
}
