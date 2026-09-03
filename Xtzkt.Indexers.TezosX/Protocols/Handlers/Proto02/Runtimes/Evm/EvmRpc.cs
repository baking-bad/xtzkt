using System.Text.Json;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class EvmRpc(EvmNode node) : Proto01.EvmRpc(node)
{
    public override async Task<(JsonElement block, JsonElement receipts, JsonElement traces)> GetBlockData(int level)
    {
        var res = await Node.PostBatchAsync(
            ("eth_getBlockByNumber", [level.ToString(), true]),
            ("eth_getBlockReceipts", [level.ToString()]),
            ("debug_traceBlockByNumber", [level.ToString(), Tracer]));

        return (res[0], res[1], res[2]);
    }
}
