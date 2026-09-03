using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Helpers;

namespace Xtzkt.Indexers.L1.Protocols.Proto13
{
    class OriginationsCommit(ProtocolHandler protocol) : Proto05.OriginationsCommit(protocol)
    {
        protected override IEnumerable<BigMapDiff>? ParseBigMapDiffs(L1OriginationOperation origination, JsonElement result, MichelineArray code, IMicheline storage)
        {
            return result.TryGetProperty("lazy_storage_diff", out var diffs)
                ? BigMapDiff.ParseLazyStorage(diffs)
                : null;
        }
    }
}
