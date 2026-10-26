using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    /// <summary>
    /// A curated binding of a ticket to the token it represents.
    /// </summary>
    public class TicketBinding
    {
        public required byte[] TicketHash { get; set; }
        public required long TokenId { get; set; }
    }

    public static class TicketBindingModel
    {
        public static void BuildTicketBindingModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<TicketBinding>()
                .HasKey(x => x.TicketHash);
            #endregion
        }
    }
}
