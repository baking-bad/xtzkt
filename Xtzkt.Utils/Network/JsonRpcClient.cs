using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtzkt.Utils.Network;

public sealed class JsonRpcClient(string url, int timeout, string? apiKeyHeader = null, string? apiKey = null) : IDisposable
{
    readonly TzktClient _client = apiKeyHeader != null && apiKey != null
        ? new(url, timeout, [(apiKeyHeader, apiKey)])
        : new(url, timeout);

    public async Task<JsonRpcResult> PostAsync(JsonRpcRequest request, CancellationToken ct)
    {
        return await _client.PostAsync<JsonRpcResult>("", JsonSerializer.Serialize(request), ct);
    }

    public async Task<JsonRpcResult<T>> PostAsync<T>(JsonRpcRequest request, CancellationToken ct)
    {
        return await _client.PostAsync<JsonRpcResult<T>>("", JsonSerializer.Serialize(request), ct);
    }

    public async Task<JsonRpcResult[]> PostBatchAsync(IEnumerable<JsonRpcRequest> requests, CancellationToken ct)
    {
        return await _client.PostAsync<JsonRpcResult[]>("", JsonSerializer.Serialize(requests), ct);
    }

    public async Task<JsonRpcResult<T>[]> PostBatchAsync<T>(IEnumerable<JsonRpcRequest> requests, CancellationToken ct)
    {
        return await _client.PostAsync<JsonRpcResult<T>[]>("", JsonSerializer.Serialize(requests), ct);
    }

    public void Dispose() => _client.Dispose();
}

public class JsonRpcRequest(int id, string method, object[] args)
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; init; } = id;

    [JsonPropertyName("method")]
    public string Method { get; init; } = method;

    [JsonPropertyName("params")]
    public object[] Params { get; init; } = args;

    public JsonRpcRequest(string method) : this(0, method, []) { }
    public JsonRpcRequest(string method, object[] args) : this(0, method, args) { }
    public JsonRpcRequest(int id, string method) : this(id, method, []) { }
}

public class JsonRpcResult : JsonRpcResult<JsonElement>;

public class JsonRpcResult<T>
{
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }

    [JsonPropertyName("result")]
    public T? Result { get; init; }

    public bool Success => Error == null;
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}