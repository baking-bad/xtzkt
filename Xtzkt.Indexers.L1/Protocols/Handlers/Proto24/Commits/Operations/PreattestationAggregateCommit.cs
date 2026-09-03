using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto24
{
    class PreattestationAggregateCommit(ProtocolHandler protocol) : Proto23.PreattestationAggregateCommit(protocol)
    {
        protected override long GetPower(JsonElement c)
        {
            var consensusPower = c.Required("consensus_power");
            return consensusPower.OptionalInt64("baking_power") ?? consensusPower.RequiredInt64("slots");
        }
    }
}
