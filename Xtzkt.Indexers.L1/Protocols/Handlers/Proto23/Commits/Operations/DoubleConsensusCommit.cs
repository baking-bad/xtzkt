using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto23
{
    class DoubleConsensusCommit(ProtocolHandler protocol) : Proto19.DoubleConsensusCommit(protocol)
    {
        protected override int GetAccusedLevel(JsonElement content)
        {
            return content.Required("metadata").Required("misbehaviour").RequiredInt32("level");
        }

        protected override string GetOffender(JsonElement content)
        {
            return content.Required("metadata").RequiredString("punished_delegate");
        }

        protected override DoubleConsensusKind GetKind(JsonElement content)
        {
            return content.Required("metadata").Required("misbehaviour").RequiredString("kind")[0] == 'a'
                ? DoubleConsensusKind.DoubleAttestation
                : DoubleConsensusKind.DoublePreattestation;
        }
    }
}
