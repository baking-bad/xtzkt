using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;

namespace Xtzkt.Indexers.L1.Protocols.Proto05
{
    class OriginationsCommit(ProtocolHandler protocol) : Proto01.OriginationsCommit(protocol)
    {
        protected override IMicheline GetCode(JsonElement content)
        {
            return Micheline.FromJson(content.Required("script").Required("code"))!;
        }

        protected override IMicheline GetStorage(JsonElement content)
        {
            return Micheline.FromJson(content.Required("script").Required("storage"))!;
        }

        protected override IEnumerable<BigMapDiff>? ParseBigMapDiffs(L1OriginationOperation origination, JsonElement result, MichelineArray code, IMicheline storage)
        {
            return result.TryGetProperty("big_map_diff", out var diffs)
                ? diffs.RequiredArray().EnumerateArray().Select(BigMapDiff.Parse)
                : null;
        }
    }
}
