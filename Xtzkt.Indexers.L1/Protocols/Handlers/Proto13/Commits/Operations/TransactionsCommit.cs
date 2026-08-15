using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Helpers;

namespace Xtzkt.Indexers.L1.Protocols.Proto13
{
    class TransactionsCommit(ProtocolHandler protocol) : Proto05.TransactionsCommit(protocol)
    {
        protected override IEnumerable<BigMapDiff>? ParseBigMapDiffs(L1TransactionOperation transaction, JsonElement result)
        {
            return result.TryGetProperty("lazy_storage_diff", out var diffs)
                ? BigMapDiff.ParseLazyStorage(diffs)
                : null;
        }
    }
}
