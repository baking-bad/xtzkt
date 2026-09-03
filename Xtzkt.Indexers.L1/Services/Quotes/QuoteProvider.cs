using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Services
{
    public interface IQuoteProvider
    {
        Task<int> FillQuotes(IEnumerable<IQuote> quotes, IQuote? last);
    }
}
