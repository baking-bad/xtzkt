using Netezos;
using Netezos.Encoding;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils.Network;

namespace Xtzkt.Indexers.TezosX.Services;

public sealed class EvmNode : IDisposable
{
    readonly TzktClient _client;
    readonly ILogger _logger;

    public EvmNode(IConfiguration config, ILogger<EvmNode> logger)
    {
        var nodeConfig = config.GetEvmNodeConfig();
        _client = new TzktClient(nodeConfig.Endpoint, nodeConfig.Timeout);
        _logger = logger;
    }

    public async Task<string> GetChainId()
    {
        var result = await PostAsync("eth_chainId");
        return result.RequiredString();
    }

    public async Task<string> GetRollupAddress()
    {
        var result = await GetAsync("evm/v2/blueprint/0");
        var payload = result.Required("blueprint").RequiredArray("payload")[0].RequiredHexBytes();
        return Base58.Convert(payload[1..21], Prefixes.sr1);
    }

    public Task<JsonElement> GetHead(bool withTxs = false)
    {
        return PostAsync("eth_getBlockByNumber", "latest", withTxs);
    }

    public Task<JsonElement> GetBlock(int level, bool withTxs = false)
    {
        return PostAsync("eth_getBlockByNumber", level.ToString(), withTxs);
    }

    public void Dispose() => _client.Dispose();

    public async Task<JsonElement> GetAsync(string path)
    {
        try
        {
            return await _client.GetAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC request ({path}) failed", path);
            throw;
        }
    }

    public async Task<JsonElement> PostAsync(string method, params object[] args)
    {
        try
        {
            var request = new JsonRpcRequest(method, args);
            var response = await _client.PostAsync("", JsonSerializer.Serialize(request));

            if (response.RequiredInt32("id") != request.Id)
                throw new Exception($"{method} response misssed");

            if (response.TryGetProperty("error", out var error))
                throw new Exception($"{method} failed with error {error.RequiredInt32("code")}: {error.RequiredString("message")}");

            return response.Required("result");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC request ({method}) failed", method);
            throw;
        }
    }

    public async Task<JsonElement[]> PostBatchAsync(params (string, object[])[] batch)
    {
        try
        {
            var requests = new JsonRpcRequest[batch.Length];
            for (int i = 0; i < batch.Length; i++)
            {
                var (method, args) = batch[i];
                requests[i] = new(i, method, args);
            }

            var responses = (await _client.PostAsync("", JsonSerializer.Serialize(requests)))
                .EnumerateArray()
                .ToDictionary(x => x.RequiredInt32("id"));

            var results = new JsonElement[batch.Length];
            for (int i = 0; i < batch.Length; i++)
            {
                var (method, _) = batch[i];

                if (!responses.TryGetValue(i, out var response))
                    throw new Exception($"{method} response misssed");

                if (response.TryGetProperty("error", out var error))
                    throw new Exception($"{method} failed with error {error.RequiredInt32("code")}: {error.RequiredString("message")}");

                results[i] = response.Required("result");
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC batch request failed");
            throw;
        }
    }

    #region debug
    public async Task<JsonElement[]> PostBatchForwardAsync((string, object[])[] batch)
    {
        try
        {
            var requests = new JsonRpcRequest[batch.Length];
            for (int i = 0; i < batch.Length; i++)
            {
                var (method, args) = batch[i];
                requests[i] = new(i, method, args);
            }

            var responses = (await _client.PostAsync("?forward=true", JsonSerializer.Serialize(requests)))
                .EnumerateArray()
                .ToDictionary(x => x.RequiredInt32("id"));

            var results = new JsonElement[batch.Length];
            for (int i = 0; i < batch.Length; i++)
            {
                var (method, _) = batch[i];

                if (!responses.TryGetValue(i, out var response))
                    throw new Exception($"{method} response misssed");

                if (response.TryGetProperty("error", out var error))
                    throw new Exception($"{method} failed with error {error.RequiredInt32("code")}: {error.RequiredString("message")}");

                results[i] = response.Required("result");
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC batch request failed");
            throw;
        }
    }
    #endregion

    class JsonRpcRequest(int id, string method, object[] args)
    {
        [JsonPropertyName("jsonrpc")]
        public string Version { get; private set; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; private set; } = id;

        [JsonPropertyName("method")]
        public string Method { get; private set; } = method;

        [JsonPropertyName("params")]
        public object[] Params { get; private set; } = args;

        public JsonRpcRequest(string method, object[] args) : this(0, method, args) { }
    }
}

public static class EvmNodeExt
{
    public static void AddEvmNode(this IServiceCollection services)
    {
        services.AddSingleton<EvmNode>();
    }
}
