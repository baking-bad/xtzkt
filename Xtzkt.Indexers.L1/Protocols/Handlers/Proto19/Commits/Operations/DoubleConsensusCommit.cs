using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class DoubleConsensusCommit(ProtocolHandler protocol) : Proto18.DoubleConsensusCommit(protocol)
    {
        protected override int GetSlashingLevel(L1Block block, L1Protocol protocol, int accusedLevel)
        {
            return Cache.Protocols.GetCycleEnd(protocol.GetCycle(accusedLevel) + protocol.SlashingDelay);
        }

        protected override DoubleConsensusKind GetKind(JsonElement content)
        {
            return content.RequiredString("kind") == "double_attestation_evidence"
                ? DoubleConsensusKind.DoubleAttestation
                : DoubleConsensusKind.DoublePreattestation;
        }
    }
}
