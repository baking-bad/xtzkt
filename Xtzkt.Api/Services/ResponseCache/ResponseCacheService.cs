using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xtzkt.Api.Filters.Base;

namespace Xtzkt.Api.Services.ResponseCache;

public class ResponseCacheService(IConfiguration configuration, IOptions<JsonOptions> options, ILogger<ResponseCacheService> logger)
{
    static readonly byte[] NullBytes = "null"u8.ToArray();

    readonly JsonSerializerOptions Options = options.Value.JsonSerializerOptions;
    readonly ILogger Logger = logger;
    readonly Dictionary<string, byte[]> Cache = new(4096);
    readonly long CacheSize = configuration.GetResponseCacheConfig().CacheSize * 1024 * 1024;
    long CacheUsed = 0;

    public bool TryGet(string key, [NotNullWhen(true)] out byte[]? response)
    {
        lock (Cache)
        {
            return Cache.TryGetValue(key, out response);
        }
    }

    public byte[] Set(string key, object? obj, bool isSerialized = false)
    {
        var bytes = obj == null
            ? NullBytes
            : isSerialized
                ? Encoding.UTF8.GetBytes((obj as string)!)
                : JsonSerializer.SerializeToUtf8Bytes(obj, Options);
        
        var size = bytes.Length + key.Length + 20; // up to 4 bytes str len, 8 bytes key ptr, 8 bytes value ptr
        if (size > CacheSize)
        {
            if (CacheSize != 0)
                Logger.LogWarning("Response size {response} exceeds cache size {cache}", size, CacheSize);
            return bytes;
        }
        
        lock (Cache)
        {
            if (CacheUsed + size > CacheSize)
            {
                Logger.LogWarning("Cache size limit reached");
                Clear(); // TODO: do not clear everything, but the oldest entries
            }

            CacheUsed += size;
            Cache[key] = bytes;
        }

        return bytes;
    }

    public void Clear()
    {
        lock (Cache)
        {
            Cache.Clear();
            CacheUsed = 0;
        }
    }

    public static string BuildKey(string? path, params (string, object?)[] query)
    {
        var sb = new StringBuilder(path);

        foreach (var (name, value) in query)
            if (value != null)
                sb.Append(value is INormalizable normalizable ? normalizable.Normalize(name) : $"{name}={value}&");

        return sb.ToString();
    }
}