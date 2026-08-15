using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto23
{
    class PreattestationAggregateCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public IEnumerable<(string, string, long)> ExtractPreattestations(JsonElement op, JsonElement content)
        {
            var res = new List<(string, string, long)>();

            var opHash = op.RequiredString("hash");
            foreach (var c in content.Required("metadata").RequiredArray("committee").EnumerateArray())
            {
                var baker = Cache.Addresses.GetExistingBaker(c.RequiredString("delegate"));
                var power = GetPower(c);
                res.Add((opHash, baker.Hash, power));
            }

            return res;
        }

        protected virtual long GetPower(JsonElement c)
        {
            return c.RequiredInt64("consensus_power");
        }
    }
}
