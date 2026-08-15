using System.Text.Json;
using Xtzkt.Indexers.Common.Services;

namespace Xtzkt.Indexers.L1.Protocols.Proto16
{
    class Rpc : Proto12.Rpc
    {
        public Rpc(TezosNode node) : base(node) { }

        public override Task<JsonElement> GetTicketBalance(int level, string address, string ticket)
        {
            return address.StartsWith("sr1")
                ? Node.PostAsync($"chains/main/blocks/{level}/context/smart_rollups/smart_rollup/{address}/ticket_balance", ticket)
                : Node.PostAsync($"chains/main/blocks/{level}/context/contracts/{address}/ticket_balance", ticket);
        }
    }
}
