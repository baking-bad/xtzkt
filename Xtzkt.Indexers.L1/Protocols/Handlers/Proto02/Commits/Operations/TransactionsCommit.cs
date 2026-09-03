using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;

namespace Xtzkt.Indexers.L1.Protocols.Proto02
{
    class TransactionsCommit(ProtocolHandler protocol) : Proto01.TransactionsCommit(protocol)
    {
        protected override IEnumerable<BigMapDiff>? ParseBigMapDiffs(L1TransactionOperation transaction, JsonElement result)
        {
            if (!result.TryGetProperty("big_map_diff", out var diffs))
                return null;

            return diffs.RequiredArray().EnumerateArray().Select(x => new UpdateDiff
            {
                Ptr = transaction.TargetId,
                KeyHash = x.RequiredExprHashBytes("key_hash"),
                Key = x.RequiredMicheline("key"),
                Value = x.OptionalMicheline("value")
            });
        }
    }
}
