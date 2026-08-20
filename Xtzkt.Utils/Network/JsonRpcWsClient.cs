using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Xtzkt.Utils.Network;

public sealed class JsonRpcWsClient(string uri, int defaultTimeout, IEnumerable<(string Name, string Value)>? headers = null)
{
    #region static
    readonly static JsonDocumentOptions DefaultDocumentOptions = new()
    {
        MaxDepth = 100_000,
    };

    const int MaxMessageLength = 8 * 1024 * 1024;
    #endregion

    readonly Uri Uri = new(uri, UriKind.Absolute);
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(defaultTimeout);
    readonly (string Name, string Value)[] Headers = headers?.ToArray() ?? [];

    public async IAsyncEnumerable<JsonElement> SubscribeAsync(
        string method,
        object[] args,
        string notificationMethod = "eth_subscription",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var socket = new ClientWebSocket();
        foreach (var (name, value) in Headers)
            socket.Options.SetRequestHeader(name, value);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        #region subscribe
        string subscription;
        try
        {
            cts.CancelAfter(Timeout);
            await socket.ConnectAsync(Uri, cts.Token);

            var request = JsonSerializer.SerializeToUtf8Bytes(new JsonRpcRequest(0, method, args));
            await socket.SendAsync(request, WebSocketMessageType.Text, true, cts.Token);

            var response = await ReceiveAsync(socket, cts.Token);

            if (response.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("code", out var c) ? c.GetRawText() : "unknown";
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                throw new Exception($"{method} failed with error {code}: {message}");
            }

            if (!response.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.String)
                throw new Exception($"{method} response missed");

            subscription = result.GetString()!;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
        #endregion

        #region listen
        while (!ct.IsCancellationRequested)
        {
            JsonElement message;
            try
            {
                cts.CancelAfter(Timeout);
                message = await ReceiveAsync(socket, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException();
            }

            if (!message.TryGetProperty("method", out var _messageMethod) ||
                _messageMethod.ValueKind != JsonValueKind.String ||
                _messageMethod.GetString() != notificationMethod ||
                !message.TryGetProperty("params", out var messageArgs) ||
                !messageArgs.TryGetProperty("subscription", out var subscriptionId) ||
                subscriptionId.ValueKind != JsonValueKind.String ||
                subscriptionId.GetString() != subscription ||
                !messageArgs.TryGetProperty("result", out var result))
                continue;

            yield return result;
        }
        #endregion
    }

    static async Task<JsonElement> ReceiveAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        while (true)
        {
            var res = await socket.ReceiveAsync(buffer.GetMemory(4096), ct);

            if (res.MessageType == WebSocketMessageType.Close)
                throw new IOException($"Websocket closed by the remote host ({socket.CloseStatus}: {socket.CloseStatusDescription})");

            buffer.Advance(res.Count);

            if (res.EndOfMessage)
                break;

            if (buffer.WrittenCount > MaxMessageLength)
                throw new IOException($"Websocket message is longer than {MaxMessageLength} bytes");
        }

        using var doc = JsonDocument.Parse(buffer.WrittenMemory, DefaultDocumentOptions);
        return doc.RootElement.Clone();
    }
}
