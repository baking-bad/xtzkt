using System.Text.Json;
using Xtzkt.Indexers.Common.Services;

namespace Xtzkt.Indexers.L1.Protocols.Initiator
{
    sealed class Rpc(TezosNode node) : Proto01.Rpc(node)
    {
        public override Task<JsonElement> GetBlockAsync(int level)
            => Node.GetAsync($"chains/main/blocks/{level}");
    }
}
