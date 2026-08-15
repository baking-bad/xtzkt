using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto22
{
    class CycleCommit : Proto21.CycleCommit
    {
        public CycleCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override long GetDalAttestationRewardPerShard(JsonElement issuance)
        {
            return issuance.RequiredInt64("dal_attesting_reward_per_shard");
        }
    }
}
