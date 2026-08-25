using Netezos.Encoding;
using System.Numerics;
using System.Text.Json;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.Common.Extensions;

public static class JsonElementExtension
{
    public static JsonElement Required(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var res) ? res
            : throw new SerializationException($"Missed required property {name}");
    }

    public static JsonElement? Optional(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var res) && res.ValueKind != JsonValueKind.Null ? res
            : null;
    }

    public static int Count(this JsonElement el)
    {
        return el.EnumerateArray().Count();
    }

    public static JsonElement RequiredArray(this JsonElement el)
    {
        return el.ValueKind == JsonValueKind.Array ? el
            : throw new SerializationException($"Expected array but got {el.ValueKind}");
    }

    public static JsonElement RequiredArray(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var res) && res.ValueKind == JsonValueKind.Array ? res
            : throw new SerializationException($"Missed required array {name}");
    }

    public static JsonElement? OptionalArray(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var res) || res.ValueKind == JsonValueKind.Null)
            return null;

        return res.ValueKind == JsonValueKind.Array ? res
            : throw new SerializationException($"Expected array but got {res.ValueKind}");
    }

    public static JsonElement RequiredArray(this JsonElement el, string name, int count)
    {
        return el.TryGetProperty(name, out var res) && res.ValueKind == JsonValueKind.Array
            && res.EnumerateArray().Count() == count ? res
                : throw new SerializationException($"Missed required array {name}[{count}]");
    }

    public static string RequiredString(this JsonElement el)
    {
        return el.ValueKind == JsonValueKind.String ? el.GetString()!
            : throw new SerializationException($"Expected string but got {el.ValueKind}");
    }

    public static string RequiredString(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var res) && res.ValueKind == JsonValueKind.String ? res.GetString()!
            : throw new SerializationException($"Missed required string {name}");
    }

    public static string? OptionalEscapedString(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var res) || res.ValueKind == JsonValueKind.Null)
            return null;

        return res.ValueKind == JsonValueKind.String ? Regexes.RestrictedUnicode().Replace(res.GetString()!, Regexes.NullEscapeString).Replace((char)0, Regexes.NullEscapeChar)
            : throw new SerializationException($"Expected string but got {res.ValueKind}");
    }

    public static string? OptionalString(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var res) || res.ValueKind == JsonValueKind.Null)
            return null;
        
        return res.ValueKind == JsonValueKind.String ? res.GetString()
            : throw new SerializationException($"Expected string but got {res.ValueKind}");
    }

    public static string? OptionalString(this JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Null)
            return null;

        return el.ValueKind == JsonValueKind.String ? el.GetString()
            : throw new SerializationException($"Expected string but got {el.ValueKind}");
    }

    public static IMicheline RequiredMicheline(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var res) ? Micheline.FromJson(res)!
            : throw new SerializationException($"Missed required {name}");
    }

    public static IMicheline? OptionalMicheline(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var res) && res.ValueKind != JsonValueKind.Null ? Micheline.FromJson(res) : null;
    }

    public static DateTime RequiredDateTime(this JsonElement el)
    {
        return el.ValueKind == JsonValueKind.String ? el.GetDateTimeOffset().UtcDateTime
            : throw new SerializationException($"Expected datetime but got {el.ValueKind}");
    }

    public static DateTime RequiredDateTime(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var res) && res.ValueKind == JsonValueKind.String ? res.GetDateTimeOffset().UtcDateTime
            : throw new SerializationException($"Missed required string {name}");
    }

    public static bool RequiredBool(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False
            ? prop.ValueKind == JsonValueKind.True
            : throw new SerializationException($"Missed required bool {name}");
    }

    public static bool? OptionalBool(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;

        return prop.ValueKind == JsonValueKind.True || (prop.ValueKind == JsonValueKind.False ? false
            : throw new SerializationException($"Invalid bool {name}"));
    }

    public static int? OptionalInt32(this JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Null)
            return null;

        return el.TryParseInt32(out var res) ? res
            : throw new SerializationException($"Expected int but got {el.ValueKind}");
    }

    public static int? OptionalInt32(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;

        return prop.TryParseInt32(out var res) ? res
            : throw new SerializationException($"Invalid int {name}");
    }

    public static long? OptionalInt64(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;

        return prop.TryParseInt64(out var res) ? res
            : throw new SerializationException($"Invalid long {name}");
    }

    public static int RequiredInt32(this JsonElement el)
    {
        return el.TryParseInt32(out var res) ? res
            : throw new SerializationException($"Expected int but got {el.ValueKind}");
    }

    public static int RequiredInt32(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.TryParseInt32(out var res) ? res
            : throw new SerializationException($"Missed required int {name}");
    }

    public static long RequiredInt64(this JsonElement el)
    {
        return el.TryParseInt64(out var res) ? res
            : throw new SerializationException($"Expected long but got {el.ValueKind}");
    }

    public static long RequiredInt64(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.TryParseInt64(out var res) ? res
            : throw new SerializationException($"Missed required long {name}");
    }

    public static DateTime RequiredHexTimestamp32(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) ? HexNumber.GetTimestamp(prop.RequiredString())
            : throw new SerializationException($"Missed required hex timestamp");
    }

    public static int RequiredHexInt32(this JsonElement el)
    {
        return HexNumber.GetInt32(el.RequiredString());
    }

    public static int RequiredHexInt32(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) ? HexNumber.GetInt32(prop.RequiredString())
            : throw new SerializationException($"Missed required hex Int32");
    }

    public static long RequiredHexInt64(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) ? HexNumber.GetInt64(prop.RequiredString())
            : throw new SerializationException($"Missed required hex Int64");
    }

    public static byte[]? OptionalHexBytes(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;

        return Hex.TryParse(prop.GetString(), out var bytes) ? bytes
            : throw new SerializationException($"Invalid hex bytes");
    }

    public static byte[] RequiredHexBytes(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && Hex.TryParse(prop.GetString(), out var bytes) ? bytes
            : throw new SerializationException($"Missed required bytes hex");
    }

    public static byte[] RequiredHexBytes(this JsonElement el)
    {
        return Hex.TryParse(el.GetString(), out var bytes) ? bytes
            : throw new SerializationException($"Missed required bytes hex");
    }

    public static BigInteger? OptionalHexBigInteger(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;

        if (prop.GetString() is not string str)
            throw new SerializationException($"Invalid HexBigInteger");

        if (str.StartsWith("0x")) str = str[2..];
        if (str.Length % 2 != 0) str = "0" + str;
        return Hex.TryParse(str, out var bytes) ? new BigInteger(bytes, true, true)
            : throw new SerializationException($"Invalid HexBigInteger");
    }

    public static BigInteger RequiredHexBigInteger(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.GetString() is not string str)
            throw new SerializationException($"Missed required hex BigInteger");

        if (str.StartsWith("0x")) str = str[2..];
        if (str.Length % 2 != 0) str = "0" + str;
        return Hex.TryParse(str, out var bytes) ? new BigInteger(bytes, true, true)
            : throw new SerializationException($"Missed required BigInteger hex");
    }

    public static BigInteger RequiredHexBigInteger(this JsonElement el)
    {
        var str = el.GetString() ?? throw new SerializationException($"Missed required BigInteger hex");
        if (str.StartsWith("0x")) str = str[2..];
        if (str.Length % 2 != 0) str = "0" + str;
        return Hex.TryParse(str, out var bytes) ? new BigInteger(bytes, true, true)
            : throw new SerializationException($"Missed required BigInteger hex");
    }

    public static BigInteger RequiredBigInteger(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && BigInteger.TryParse(prop.GetString(), out var res) ? res
            : throw new SerializationException($"Missed required BigInteger {name}");
    }

    public static BigInteger? OptionalBigInteger(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;

        return BigInteger.TryParse(prop.GetString(), out var res) ? res
            : throw new SerializationException($"Invalid BigInteger {name}");
    }

    public static bool TryGetBigInteger(this JsonElement el, string name, out BigInteger res)
    {
        res = BigInteger.Zero;
        return el.TryGetProperty(name, out var prop) && BigInteger.TryParse(prop.GetString(), out res);
    }

    public static int ParseInt32(this JsonElement el)
    {
        return el.ValueKind == JsonValueKind.String
            ? int.Parse(el.GetString()!)
            : el.GetInt32();
    }

    public static bool TryParseInt32(this JsonElement el, out int res)
    {
        return el.ValueKind == JsonValueKind.String
            ? int.TryParse(el.GetString(), out res)
            : el.TryGetInt32(out res);
    }

    public static long ParseInt64(this JsonElement el)
    {
        return el.ValueKind == JsonValueKind.String
            ? long.Parse(el.GetString()!)
            : el.GetInt64();
    }

    public static bool TryParseInt64(this JsonElement el, out long res)
    {
        return el.ValueKind == JsonValueKind.String
            ? long.TryParse(el.GetString(), out res)
            : el.TryGetInt64(out res);
    }
}
