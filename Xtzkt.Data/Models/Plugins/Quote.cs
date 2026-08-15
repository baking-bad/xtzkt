using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models
{
    public interface IQuote
    {
        int Level { get; }
        DateTime Timestamp { get; }

        double Btc { get; set; }
        double Eur { get; set; }
        double Usd { get; set; }
        double Cny { get; set; }
        double Jpy { get; set; }
        double Krw { get; set; }
        double Eth { get; set; }
        double Gbp { get; set; }
    }

    public class Quote : IQuote
    {
        public required int ChainId { get; set; }
        public required int Level { get; set; }
        public required DateTime Timestamp { get; set; }

        public double Btc { get; set; }
        public double Eur { get; set; }
        public double Usd { get; set; }
        public double Cny { get; set; }
        public double Jpy { get; set; }
        public double Krw { get; set; }
        public double Eth { get; set; }
        public double Gbp { get; set; }
    }

    public static class QuoteModel
    {
        public static void BuildQuoteModel(this ModelBuilder modelBuilder)
        {
            #region keys
            modelBuilder.Entity<Quote>()
                .HasKey(x => new { x.ChainId, x.Level });
            #endregion
        }
    }
}
