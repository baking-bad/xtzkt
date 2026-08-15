using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class StakingCommit : Proto18.StakingCommit
    {
        public StakingCommit(ProtocolHandler protocol) : base(protocol) { }

        protected override string GetFreezerBaker(JsonElement update)
        {
            return update.Required("staker").RequiredString("baker_own_stake");
        }
    }
}
