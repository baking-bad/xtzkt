using System.Text.Json.Serialization;
using Xtzkt.Utils.Encoding;
using Xtzkt.Utils.Network;

namespace Xtzkt.Services.Metadata.Utils;

public sealed class EvmRpcClient(string url, int timeoutSec, string? apiKeyHeader = null, string? apiKey = null) : IDisposable
{
    readonly JsonRpcClient _client = new(url, timeoutSec, apiKeyHeader, apiKey);

    public async Task<string> EthChainIdAsync(CancellationToken ct)
    {
        var result = await _client.PostAsync<string>(new("eth_chainId"), ct);
        
        if (result.Error != null)
            throw new Exception($"Method failed with {result.Error.Code}: {result.Error.Message}");

        if (result.Result is not string chainId)
            throw new Exception("Method returned no result");

        return chainId;
    }

    public async Task<EthCallResult[]> BatchEthCallAsync(IReadOnlyList<EthCallParams> calls, CancellationToken ct)
    {
        if (calls.Count == 0)
            return [];

        var requests = new JsonRpcRequest[calls.Count];
        for (int i = 0; i < calls.Count; i++)
            requests[i] = new JsonRpcRequest(i, "eth_call", [calls[i], "latest"]);

        var results = await _client.PostBatchAsync<string>(requests, ct);
        var resultsDict = new Dictionary<int, JsonRpcResult<string>>(results.Length);
        foreach (var result in results)
            if (result.Id is int id)
                resultsDict[id] = result;

        var callResults = new EthCallResult[calls.Count];
        for (int i = 0; i < calls.Count; i++)
        {
            if (!resultsDict.TryGetValue(i, out var result))
            {
                callResults[i] = EthCallResult.Failed(null, "Missing response");
                continue;
            }

            if (result.Error != null)
            {
                if (result.Error.Code == 3 || result.Error.Message?.Contains("revert", StringComparison.OrdinalIgnoreCase) == true)
                {
                    callResults[i] = EthCallResult.Reverted(result.Error.Code, result.Error.Message);
                    continue;
                }
                else
                {
                    callResults[i] = EthCallResult.Failed(result.Error.Code, result.Error.Message);
                    continue;
                }
            }

            if (result.Result is not string hex)
            {
                callResults[i] = EthCallResult.Failed(null, "Missing result");
                continue;
            }

            callResults[i] = EthCallResult.Success(Hex.GetBytes(hex));
        }

        return callResults;
    }

    public void Dispose() => _client.Dispose();

}

public class EthCallParams(string to, string data)
{
    [JsonPropertyName("to")]
    public string To { get; } = to;

    [JsonPropertyName("data")]
    public string Data { get; } = data;
}

public enum EthCallStatus
{
    Success,
    Revert,
    Fail,
}

public readonly record struct EthCallResult(EthCallStatus Status, byte[]? Data, int? ErrorCode, string? ErrorMessage)
{
    public static EthCallResult Success(byte[] data) => new(EthCallStatus.Success, data, null, null);
    public static EthCallResult Reverted(int? code, string? message) => new(EthCallStatus.Revert, null, code, message);
    public static EthCallResult Failed(int? code, string? message) => new(EthCallStatus.Fail, null, code, message);
}
