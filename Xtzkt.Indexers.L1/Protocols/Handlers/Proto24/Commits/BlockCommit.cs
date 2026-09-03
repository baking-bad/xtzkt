using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto24
{
    class BlockCommit(ProtocolHandler protocol) : Proto19.BlockCommit(protocol)
    {
        public override async Task Apply(JsonElement rawBlock)
        {
            await base.Apply(rawBlock);

            var state = Cache.Chain.Get();
            if (state.AbaActivationLevel is null)
            {
                var abaLevel = rawBlock.Required("metadata").Optional("all_bakers_attest_activation_level")?.RequiredInt32("level");
                if (abaLevel == Block.Level)
                    state.AbaActivationLevel = abaLevel;
            }
        }

        public override void Revert (L1Block block)
        {
            var state = Cache.Chain.Get();
            if (state.AbaActivationLevel == block.Level)
                state.AbaActivationLevel = null;

            base.Revert(block);
        }

        protected override long GetAttestationCommittee(L1Protocol protocol, JsonElement metadata)
            => metadata.Optional("attestations")?.RequiredInt64("total_committee_power") ?? 0L;
    }
}
