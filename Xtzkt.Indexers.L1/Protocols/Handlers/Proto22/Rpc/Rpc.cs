using System.Text.Json;
using Xtzkt.Indexers.Common.Services;

namespace Xtzkt.Indexers.L1.Protocols.Proto22
{
    class Rpc : Proto19.Rpc
    {
        public Rpc(TezosNode node) : base(node) { }

        public override Task<JsonElement> GetDelegateDalParticipationAsync(int level, string address)
            => Node.GetAsync($"chains/main/blocks/{level}/context/delegates/{address}/dal_participation");
    }
}
