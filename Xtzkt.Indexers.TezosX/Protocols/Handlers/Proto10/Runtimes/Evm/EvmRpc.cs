using System.Text.Json;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class EvmRpc(EvmNode node) : IEvmRpc
{
    static readonly object Tracer = new { tracer = "callTracer", onlyTopCall = false, withLog = true };

    protected readonly EvmNode Node = node;

    public async Task<(JsonElement block, JsonElement receipts, JsonElement traces)> GetBlockData(int level)
    {
        var res = await Node.PostBatchAsync(
            ("eth_getBlockByNumber", [level.ToString(), true]),
            ("eth_getBlockReceipts", [level.ToString()]),
            ("debug_traceBlockByNumber", [level.ToString(), Tracer]));

        return (res[0], res[1], res[2]);
    }

    public Task<JsonElement> GetBlueprint(int level)
    {
        return Node.GetAsync($"evm/v2/blueprint/{level}");
    }

    public Task<JsonElement> GetMichelsonActivationLevel()
    {
        return Node.PostAsync("tez_getMichelsonActivationLevel");
    }

    public Task<JsonElement> GetBalance(string address, int level)
    {
        return Node.PostAsync("eth_getBalance", address, level.ToString());
    }

    public Task<JsonElement[]> GetBalance(IEnumerable<string> addresses, int level)
    {
        return Node.PostBatchAsync([.. addresses.Select(x => ("eth_getBalance", new object[] { x, level.ToString() }))]);
    }

    public Task<JsonElement[]> GetNonce(IEnumerable<string> addresses, int level)
    {
        return Node.PostBatchAsync([.. addresses.Select(x => ("eth_getTransactionCount", new object[] { x, level.ToString() }))]);
    }

    public Task<JsonElement[]> GetCode(IEnumerable<string> addresses, int level)
    {
        return Node.PostBatchAsync([.. addresses.Select(x => ("eth_getCode", new object[] { x, level.ToString() }))]);
    }

    public Task<JsonElement> GetTransactionCount(string address, int level)
    {
        return Node.PostAsync("eth_getTransactionCount", address, level.ToString());
    }

    public Task<JsonElement> GetBalanceEarliest(string address)
    {
        return Node.PostAsync("eth_getBalance", address, "earliest");
    }

    public Task<JsonElement> GetCodeEarliest(string address)
    {
        return Node.PostAsync("eth_getCode", address, "earliest");
    }

    public Task<JsonElement> GetCode(string address, int level)
    {
        return Node.PostAsync("eth_getCode", address, level.ToString());
    }
}
