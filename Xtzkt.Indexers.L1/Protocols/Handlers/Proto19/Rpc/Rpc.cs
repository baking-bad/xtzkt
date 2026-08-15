using System.Text.Json;
using Xtzkt.Indexers.Common.Services;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    class Rpc : Proto18.Rpc
    {
        public Rpc(TezosNode node) : base(node) { }

        public override Task<JsonElement> GetCurrentStakingBalance(int level, string address)
            => Node.GetAsync($"chains/main/blocks/{level}/context/raw/json/staking_balance/{address}");
    }
}
