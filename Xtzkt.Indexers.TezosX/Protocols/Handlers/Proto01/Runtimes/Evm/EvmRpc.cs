using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class EvmRpc(EvmNode node) : IEvmRpc
{
    protected static readonly object Tracer = new { tracer = "callTracer", onlyTopCall = false, withLog = true };

    protected readonly EvmNode Node = node;

    public virtual async Task<(JsonElement block, JsonElement receipts, JsonElement traces)> GetBlockData(int level)
    {
        var res = await Node.PostBatchRawAsync(
            ("eth_getBlockByNumber", [level.ToString(), true]),
            ("eth_getBlockReceipts", [level.ToString()]),
            ("debug_traceBlockByNumber", [level.ToString(), Tracer]));

        if (res[0].Error is JsonElement error0)
            throw new Exception($"eth_getBlockByNumber failed with error {error0.RequiredInt32("code")}: {error0.RequiredString("message")}");

        if (res[1].Error is JsonElement error1)
            throw new Exception($"eth_getBlockReceipts failed with error {error1.RequiredInt32("code")}: {error1.RequiredString("message")}");

        if (res[2].Error is JsonElement error2)
        {
            if (error2.RequiredInt32("code") != -32603) // tracer is not activated
                throw new Exception($"debug_traceBlockByNumber failed with error {error2.RequiredInt32("code")}: {error2.RequiredString("message")}");

            var emptyTraces = res[1].Result!.Value
                .EnumerateArray()
                .Select(x => x.RequiredString("transactionHash"))
                .Select(x => new { txHash = x, result = (object?)null });

            res[2] = (JsonSerializer.SerializeToElement(emptyTraces), null);
        }

        return (res[0].Result!.Value, res[1].Result!.Value, res[2].Result!.Value);
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
