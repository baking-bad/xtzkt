using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto24
{
    class CycleCommit(ProtocolHandler protocol) : Proto22.CycleCommit(protocol)
    {
        protected override long GetBlockBonusPerBlock(JsonElement issuance, L1Protocol protocol)
            => issuance.RequiredInt64("baking_reward_bonus_per_block");

        protected override long GetAttestationRewardPerBlock(JsonElement issuance, L1Protocol protocol)
            => issuance.RequiredInt64("attesting_reward_per_block");
    }
}
