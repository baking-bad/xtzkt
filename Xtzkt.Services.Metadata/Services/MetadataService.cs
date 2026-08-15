using System.Buffers;
using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Services.Metadata.Models;
using Xtzkt.Services.Metadata.Utils;
using Xtzkt.Utils;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Services.Metadata.Services;

public sealed class MetadataConfig
{
    public int MaxDepth { get; set; } = 10_000;
    public int MaxSize { get; set; } = 10_485_760;
}

public class MetadataService(IConfiguration config, ILogger<MetadataService> logger)
{
    readonly MetadataConfig _config = config.GetSection("Metadata").Get<MetadataConfig>() ?? new();
    readonly ILogger _logger = logger;

    public string SanitizeJson(ReadOnlySpan<byte> bytes, string? idReplacer = null)
    {
        var json = Regexes.RestrictedUnicode().Replace(Utf8.GetString(bytes), Regexes.NullEscapeString);
        if (idReplacer is string id) json = json.Replace("{id}", id);
        return json;
    }

    public (string? Name, string? Symbol, int? Decimals) ParseTokenIdentity(string json)
    {
        string? name = null;
        string? symbol = null;
        int? decimals = null;

        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = _config.MaxDepth });
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("name", out var np) && TryParseName(np, out var n))
                name = n;

            if (root.TryGetProperty("symbol", out var sp) && TryParseSymbol(sp, out var s))
                symbol = s;

            if (root.TryGetProperty("decimals", out var dp) && TryParseDecimals(dp, out var d))
                decimals = d;
        }

        return (name, symbol, decimals);
    }

    public (TokenMetadataStatus Status, string? Name, string? Symbol, int? Decimals, string? Json) FromJsonElement(JsonElement? metadata)
    {
        if (metadata is not JsonElement jsonElement)
            return (TokenMetadataStatus.InvalidJson, null, null, null, null);

        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = _config.MaxDepth });
            jsonElement.WriteTo(writer);
        }
        catch (InvalidOperationException)
        {
            return (TokenMetadataStatus.DepthLimitExceeded, null, null, null, null);
        }

        if (buffer.WrittenCount > _config.MaxSize)
        {
            return (TokenMetadataStatus.SizeLimitExceeded, null, null, null, null);
        }

        var json = SanitizeJson(buffer.WrittenSpan);
        var (name, symbol, decimals) = ParseTokenIdentity(json);
        return (TokenMetadataStatus.Ok, name, symbol, decimals, json);
    }

    public async Task<TokenMetadata> FromHttpResponse(HttpResponseMessage response, TokenLinkInfo token, DateTime syncedAt, bool withPlaceholder, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Failed to fetch #{id}, status code: {code}", token.Id, response.StatusCode);
            return new TokenMetadata(token.Id, token.Status + 1, syncedAt);
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength != null && contentLength > _config.MaxSize)
        {
            _logger.LogDebug("Failed to fetch #{id}, content length: {len}", token.Id, contentLength);
            return new TokenMetadata(token.Id, TokenMetadataStatus.SizeLimitExceeded, syncedAt);
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream(Math.Min((int)(contentLength ?? 4096), 16384));
            var chunk = ArrayPool<byte>.Shared.Rent(8192);

            try
            {
                int read;
                while ((read = await stream.ReadAsync(chunk, ct)) > 0)
                {
                    if (buffer.Length + read > _config.MaxSize)
                    {
                        _logger.LogDebug("Failed to fetch #{id}, buffer length: {len}", token.Id, buffer.Length + read);
                        return new TokenMetadata(token.Id, TokenMetadataStatus.SizeLimitExceeded, syncedAt);
                    }

                    buffer.Write(chunk, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(chunk);
            }

            return FromBytes(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), token.Id, token.TokenId, syncedAt, withPlaceholder);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch #{id}", token.Id);
            return new TokenMetadata(token.Id, token.Status + 1, syncedAt);
        }
    }

    public TokenMetadata FromDataUri(string uri, TokenInfo token, DateTime syncedAt, bool withPlaceholder)
    {
        var commaIndex = uri.IndexOf(',');
        if (commaIndex < 0 || commaIndex == uri.Length - 1)
        {
            _logger.LogDebug("Failed to fetch #{id}, invalid data uri", token.Id);
            return new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
        }

        var isBase64 = uri.AsSpan(0, commaIndex).EndsWith(";base64", StringComparison.OrdinalIgnoreCase);

        var payloadLength = uri.Length - commaIndex - 1;
        if (isBase64 && payloadLength / 4 * 3 > _config.MaxSize + 3 || !isBase64 && payloadLength > _config.MaxSize)
        {
            _logger.LogDebug("Failed to fetch #{id}, data uri (base64: {isBase64}) length: ", isBase64, payloadLength);
            return new TokenMetadata(token.Id, TokenMetadataStatus.SizeLimitExceeded, syncedAt);
        }

        try
        {
            var payload = uri[(commaIndex + 1)..];

            var bytes = isBase64
                ? Convert.FromBase64String(payload)
                : Utf8.GetBytes(Uri.UnescapeDataString(payload));

            if (bytes.Length > _config.MaxSize)
            {
                _logger.LogDebug("Failed to fetch #{id}, data uri bytes length: {len}", token.Id, bytes.Length);
                return new TokenMetadata(token.Id, TokenMetadataStatus.SizeLimitExceeded, syncedAt);
            }

            return FromBytes(bytes, token.Id, token.TokenId, syncedAt, withPlaceholder);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch #{id}", token.Id);
            return new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
        }
    }

    TokenMetadata FromBytes(ReadOnlySpan<byte> bytes, long id, BigInteger tokenId, DateTime syncedAt, bool withPlaceholder)
    {
        #region validate
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = _config.MaxDepth });
        var empty = true;
        try
        {
            while (reader.Read())
                empty = false;

            if (empty)
            {
                _logger.LogDebug("Failed to fetch #{id}, empty json", id);
                return new TokenMetadata(id, TokenMetadataStatus.InvalidJson, syncedAt);
            }
        }
        catch (Exception ex)
        {
            if (reader.CurrentDepth >= _config.MaxDepth)
            {
                _logger.LogDebug("Failed to fetch #{id}, json depth: {depth}", id, reader.CurrentDepth);
                return new TokenMetadata(id, TokenMetadataStatus.DepthLimitExceeded, syncedAt);
            }

            _logger.LogDebug(ex, "Failed to fetch #{id}", id);
            return new TokenMetadata(id, TokenMetadataStatus.InvalidJson, syncedAt);
        }
        #endregion

        var json = SanitizeJson(bytes, withPlaceholder ? Erc1155.TokenIdToHex64(tokenId) : null);
        var (name, symbol, decimals) = ParseTokenIdentity(json);
        _logger.LogDebug("Metadata for #{id} fetched", id);

        return new TokenMetadata(id, TokenMetadataStatus.Ok, syncedAt, name, symbol, decimals, json);
    }

    static bool TryParseName(JsonElement json, out string? value)
    {
        value = json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.GetRawText(),
            _ => null
        };
        return value != null;
    }

    static bool TryParseSymbol(JsonElement json, out string? value)
    {
        value = json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.GetRawText(),
            _ => null
        };
        return value != null;
    }

    static bool TryParseDecimals(JsonElement json, out int value)
    {
        value = 0;
        return json.ValueKind switch
        {
            JsonValueKind.Number => json.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(json.GetString(), out value),
            _ => false
        };
    }
}
