using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public class Ticket
    {
        public required long Id { get; set; }
        public required int ChainId { get; set; }
        public required int TicketerId { get; set; }

        public required int FirstMinterId { get; set; }
        public required int FirstLevel { get; set; }
        public required DateTime FirstTimestamp { get; set; }
        public required int LastLevel { get; set; }
        public required DateTime LastTimestamp { get; set; }

        public int TransfersCount { get; set; }
        public int BalancesCount { get; set; }
        public int HoldersCount { get; set; }

        public BigInteger TotalMinted { get; set; }
        public BigInteger TotalBurned { get; set; }
        public BigInteger TotalSupply { get; set; }

        public int TypeHash { get; set; }
        public int ContentHash { get; set; }
        
        public required byte[] RawType { get; set; }
        public required byte[] RawContent { get; set; }
        public string? JsonContent { get; set; }
    }
    
    public static class TicketModel
    {
        public static void BuildTicketModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<Ticket>()
                .HasKey(x => x.Id);
            #endregion

            #region props
            modelBuilder.Entity<Ticket>()
                .Property(x => x.JsonContent)
                .HasColumnType("jsonb");
            #endregion

            #region indexes
            modelBuilder.Entity<Ticket>()
                .HasIndex(x => new { x.TicketerId, x.TypeHash, x.ContentHash });
            #endregion
        }
    }
}
