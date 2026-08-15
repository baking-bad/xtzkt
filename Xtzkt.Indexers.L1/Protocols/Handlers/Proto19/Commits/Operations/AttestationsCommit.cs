using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class AttestationsCommit(ProtocolHandler protocol) : Proto12.AttestationsCommit(protocol)
    {
        protected override long GetPower(JsonElement metadata)
        {
            return metadata.RequiredInt64("consensus_power");
        }
    }
}
