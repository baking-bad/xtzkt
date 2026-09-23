using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
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

        var bytes = JsonMarshal.GetRawUtf8Value(jsonElement);

        if (bytes.Length > _config.MaxSize)
            return (TokenMetadataStatus.SizeLimitExceeded, null, null, null, null);

        if (Validate(bytes, null, out _) is TokenMetadataStatus errorStatus)
            return (errorStatus, null, null, null, null);

        var json = SanitizeJson(bytes);
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
            _logger.LogDebug("Failed to fetch #{id}, data uri (base64: {isBase64}) length: {len}", token.Id, isBase64, payloadLength);
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
        var bom = System.Text.Encoding.UTF8.Preamble;
        if (bytes.StartsWith(bom))
            bytes = bytes[bom.Length..];

        var idReplacer = withPlaceholder ? Erc1155.TokenIdToHex64(tokenId) : null;
        if (Validate(bytes, idReplacer, out var error) is TokenMetadataStatus errorStatus)
        {
            _logger.LogDebug(error, "Failed to fetch #{id}, {status}", id, errorStatus);
            return new TokenMetadata(id, errorStatus, syncedAt);
        }

        var json = SanitizeJson(bytes, idReplacer);
        var (name, symbol, decimals) = ParseTokenIdentity(json);
        _logger.LogDebug("Metadata for #{id} fetched", id);

        return new TokenMetadata(id, TokenMetadataStatus.Ok, syncedAt, name, symbol, decimals, json);
    }

    /// <summary>
    /// Returns null if the JSON can be stored as jsonb, parsed back within MaxDepth, and stays within MaxSize
    /// with its id placeholders filled in and its numbers printed in full, or the error status otherwise.
    /// </summary>
    TokenMetadataStatus? Validate(ReadOnlySpan<byte> bytes, string? idReplacer, out Exception? error)
    {
        error = null;

        // SanitizeJson puts the token id in place of every {id}, which makes 64 bytes out of 4
        var size = (long)bytes.Length;
        if (idReplacer != null)
            size += (long)bytes.Count("{id}"u8) * (idReplacer.Length - "{id}".Length);

        if (size > _config.MaxSize)
            return TokenMetadataStatus.SizeLimitExceeded;

        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = _config.MaxDepth + 1 });
        var empty = true;
        try
        {
            while (reader.Read())
            {
                empty = false;

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject or JsonTokenType.StartArray when reader.CurrentDepth >= _config.MaxDepth:
                        return TokenMetadataStatus.DepthLimitExceeded;

                    // a well-formed escape can still make an invalid UTF-16 string (a lone surrogate), which jsonb rejects
                    case JsonTokenType.String or JsonTokenType.PropertyName when reader.ValueIsEscaped && Jsonb.HasLoneSurrogate(reader.ValueSpan):
                        return TokenMetadataStatus.InvalidJson;

                    // so does a number out of the numeric range, which the JSON grammar doesn't limit
                    case JsonTokenType.Number:
                        if (!Jsonb.IsValidNumber(reader.ValueSpan, out var length))
                            return TokenMetadataStatus.InvalidJson;

                        // and jsonb prints it back without the exponent, so 8 bytes of 1e131071 come back as 131072 digits
                        size += length - reader.ValueSpan.Length;
                        if (size > _config.MaxSize)
                            return TokenMetadataStatus.SizeLimitExceeded;
                        break;
                }
            }

            return empty ? TokenMetadataStatus.InvalidJson : null;
        }
        catch (Exception ex)
        {
            error = ex;
            return TokenMetadataStatus.InvalidJson;
        }
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
