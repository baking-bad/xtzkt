using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtzkt.Utils.Network;

public sealed class TzktClient : IDisposable
{
    #region static
    readonly static JsonSerializerOptions DefaultSerializerOptions = new()
    {
        MaxDepth = 100_000,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    readonly static JsonDocumentOptions DefaultDocumentOptions = new()
    {
        MaxDepth = 100_000,
    };
    #endregion

    readonly HttpClient HttpClient;
    readonly TimeSpan Timeout;

    public TzktClient(int defaultTimeout, IEnumerable<(string Name, string Value)>? headers = null)
        : this((Uri?)null, defaultTimeout, headers) { }

    public TzktClient(string baseUri, int defaultTimeout, IEnumerable<(string Name, string Value)>? headers = null)
        : this(new Uri($"{baseUri.TrimEnd('/')}/", UriKind.Absolute), defaultTimeout, headers) { }

    TzktClient(Uri? baseAddress, int defaultTimeout, IEnumerable<(string Name, string Value)>? headers = null)
    {
        HttpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        })
        {
            BaseAddress = baseAddress,
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            DefaultRequestHeaders =
            {
                Accept = { new MediaTypeWithQualityHeaderValue("application/json") },
                UserAgent = { new ProductInfoHeaderValue(AssemblyInfo.Name, AssemblyInfo.Version) }
            },
        };

        Timeout = TimeSpan.FromSeconds(defaultTimeout);

        if (headers != null)
            foreach (var (name, value) in headers)
                HttpClient.DefaultRequestHeaders.Add(name, value);
    }

    public async IAsyncEnumerable<JsonElement> GetStreamAsync(string path, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stream = await GetStreamAsync(path);
        using var reader = new StreamReader(stream);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        while (!ct.IsCancellationRequested)
        {
            JsonElement result;
            try
            {
                cts.CancelAfter(Timeout);
                var json = await reader.ReadLineAsync(cts.Token);
                if (json == null) break;
                using var doc = JsonDocument.Parse(json, DefaultDocumentOptions);
                result = doc.RootElement.Clone();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
            yield return result;
        }
    }

    public async IAsyncEnumerable<T> GetStreamAsync<T>(string path, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stream = await GetStreamAsync(path);
        using var reader = new StreamReader(stream);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        while (!ct.IsCancellationRequested)
        {
            T result;
            try
            {
                cts.CancelAfter(Timeout);
                var json = await reader.ReadLineAsync(cts.Token);
                if (json == null) break;
                result = JsonSerializer.Deserialize<T>(json, DefaultSerializerOptions)!;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
            yield return result;
        }
    }

    public async Task<JsonElement> GetAsync(string path)
    {
        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            using var stream = await HttpClient.GetStreamAsync(path, cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, DefaultDocumentOptions, cts.Token);
            return doc.RootElement.Clone();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public async Task<JsonElement> GetAsync(string path, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            using var stream = await HttpClient.GetStreamAsync(path, cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, DefaultDocumentOptions, cts.Token);
            return doc.RootElement.Clone();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public async Task<T> GetAsync<T>(string path)
    {
        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            using var stream = await HttpClient.GetStreamAsync(path, cts.Token);
            return (await JsonSerializer.DeserializeAsync<T>(stream, DefaultSerializerOptions, cts.Token))!;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public async Task<JsonElement> PostAsync(string path, string content, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            using var response = (await HttpClient.PostAsync(path, new JsonContent(content), cts.Token)).EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, DefaultDocumentOptions, cts.Token);
            return doc.RootElement.Clone();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public async Task<T> PostAsync<T>(string path, string content, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            using var response = (await HttpClient.PostAsync(path, new JsonContent(content), cts.Token)).EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            return (await JsonSerializer.DeserializeAsync<T>(stream, DefaultSerializerOptions, cts.Token))!;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    async Task<Stream> GetStreamAsync(string path)
    {
        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            return await HttpClient.GetStreamAsync(path, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    public void Dispose() => HttpClient?.Dispose();
}
