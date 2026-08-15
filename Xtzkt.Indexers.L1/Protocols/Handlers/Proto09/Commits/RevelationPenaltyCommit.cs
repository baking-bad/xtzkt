using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto09
{
    class RevelationPenaltyCommit : Proto06.RevelationPenaltyCommit
    {
        public RevelationPenaltyCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override bool HasPenaltiesUpdates(L1Block block, L1Protocol protocol, JsonElement rawBlock)
        {
            return rawBlock
                .Required("metadata")
                .RequiredArray("balance_updates")
                .EnumerateArray()
                .Any(x => x.RequiredString("origin")[0] == 'b' &&
                          x.RequiredString("kind")[0] == 'f' &&
                          x.RequiredInt64("change") < 0 &&
                          GetFreezerCycle(x) != block.Cycle - protocol.ConsensusRightsDelay);
        }
    }
}
