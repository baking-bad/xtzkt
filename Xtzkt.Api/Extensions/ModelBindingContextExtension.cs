using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Netezos.Encoding;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Utils;
using Xtzkt.Utils;
using Base58 = Xtzkt.Utils.Encoding.Base58;
using Hex = Xtzkt.Utils.Encoding.Hex;

namespace Xtzkt.Api.Extensions;

internal static class ModelBindingContextExtension
{
    public static bool TryGetEnum<T>(this ModelBindingContext bindingContext, Dictionary<string, T> map, string name, ref bool hasValue, out T? result) where T : struct
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!map.TryGetValue(rawValue, out var value))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = value;
        }
        return true;
    }

    public static bool TryGetEnumList<T>(this ModelBindingContext bindingContext, Dictionary<string, T> map, string name, ref bool hasValue, out List<T>? result) where T : struct
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<T>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!map.TryGetValue(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetHexBytes(this ModelBindingContext bindingContext, string name, ref bool hasValue, out byte[]? result, int? length = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (length is int len && rawValue.Length != len + 2)
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                return false;
            }
            if (!Xtzkt.Utils.Encoding.Hex.TryGetBytes(rawValue, out var bytes))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = bytes;
        }
        return true;
    }

    public static bool TryGetHexBytesList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<byte[]>? result, int? length = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<byte[]>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (length is int len && rawValue.Length != len + 2)
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                    return false;
                }
                if (!Xtzkt.Utils.Encoding.Hex.TryGetBytes(rawValue, out var bytes))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                list.Add(bytes);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetBase58(this ModelBindingContext bindingContext, string name, ref bool hasValue, out string? result, string prefix, int? length = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (length is int len && rawValue.Length != len)
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                return false;
            }
            if (!rawValue.StartsWith(prefix) || !Regexes.Base58().IsMatch(rawValue))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = rawValue;
        }
        return true;
    }

    public static bool TryGetBase58List(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<string>? result, string prefix, int? length = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<string>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (length is int len && rawValue.Length != len)
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                    return false;
                }
                if (!rawValue.StartsWith(prefix) || !Regexes.Base58().IsMatch(rawValue))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                list.Add(rawValue);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetBase58Bytes(this ModelBindingContext bindingContext, string name, ref bool hasValue, out byte[]? result, byte[] base58Prefix)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!Base58.TryDecode(rawValue, base58Prefix, out result))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
        }
        return true;
    }

    public static bool TryGetBase58BytesList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<byte[]>? result, byte[] base58Prefix)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<byte[]>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!Base58.TryDecode(rawValue, base58Prefix, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetHexOrBase58(this ModelBindingContext bindingContext, string name, ref bool hasValue, out string? result, string base58Prefix, int? base58Length = null, int? hexLength = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (Regexes.Hex().IsMatch(rawValue))
            {
                if (hexLength is int len && rawValue.Length != len + 2)
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                    return false;
                }
                // hex values are always stored lowercase, while base58 ones are case-sensitive
                rawValue = rawValue.ToLowerInvariant();
            }
            else if (rawValue.StartsWith(base58Prefix) && Regexes.Base58().IsMatch(rawValue))
            {
                if (base58Length is int len && rawValue.Length != len)
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                    return false;
                }
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = rawValue;
        }
        return true;
    }

    public static bool TryGetHexOrBase58List(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<string>? result, string base58Prefix, int? base58Length = null, int? hexLength = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<string>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                var value = rawValue;
                if (Regexes.Hex().IsMatch(value))
                {
                    if (hexLength is int len && value.Length != len + 2)
                    {
                        bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                        return false;
                    }
                    // hex values are always stored lowercase, while base58 ones are case-sensitive
                    value = value.ToLowerInvariant();
                }
                else if (value.StartsWith(base58Prefix) && Regexes.Base58().IsMatch(value))
                {
                    if (base58Length is int len && value.Length != len)
                    {
                        bindingContext.ModelState.TryAddModelError(name, "Invalid value length.");
                        return false;
                    }
                }
                else
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetHexOrBase58Bytes(this ModelBindingContext bindingContext, string name, ref bool hasValue, out byte[]? result, byte[] base58Prefix, int? hexLength = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (rawValue.StartsWith("0x"))
            {
                if (hexLength is int len && rawValue.Length != len + 2)
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid length.");
                    return false;
                }
                if (!Hex.TryGetBytes(rawValue, out result))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
            }
            else if (!Base58.TryDecode(rawValue, base58Prefix, out result))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
        }
        return true;
    }

    public static bool TryGetHexOrBase58BytesList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<byte[]>? result, byte[] base58Prefix, int? hexLength = null)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<byte[]>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                byte[]? value;
                if (rawValue.StartsWith("0x"))
                {
                    if (hexLength is int len && rawValue.Length != len + 2)
                    {
                        bindingContext.ModelState.TryAddModelError(name, "Invalid length.");
                        return false;
                    }
                    if (!Hex.TryGetBytes(rawValue, out value))
                    {
                        bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                        return false;
                    }
                }
                else if (!Base58.TryDecode(rawValue, base58Prefix, out value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetAddressHashNull(this ModelBindingContext bindingContext, string name, ref bool hasValue, out string? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (rawValue == "null")
            {
                hasValue = true;
                result = AddressHashNullParameter.Null;
            }
            else
            {
                if (!AddressHash.TryNormalize(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                hasValue = true;
                result = value;
            }
        }
        return true;
    }

    public static bool TryGetAddressHashNullList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<string>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<string>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (rawValue == "null")
                {
                    list.Add(AddressHashNullParameter.Null);
                }
                else
                {
                    if (!AddressHash.TryNormalize(rawValue, out var value))
                    {
                        bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                        return false;
                    }
                    list.Add(value);
                }
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetAddressHash(this ModelBindingContext bindingContext, string name, ref bool hasValue, out string? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!AddressHash.TryNormalize(rawValue, out var value))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = value;
        }
        return true;
    }

    public static bool TryGetAddressHashList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<string>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<string>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!AddressHash.TryNormalize(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetInt32Null(this ModelBindingContext bindingContext, string name, ref bool hasValue, out int? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (rawValue == "null")
            {
                hasValue = true;
                result = Int32NullParameter.Null;
            }
            else
            {
                if (!int.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                hasValue = true;
                result = value;
            }
        }
        return true;
    }

    public static bool TryGetInt32NullList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<int>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<int>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (rawValue == "null")
                {
                    list.Add(Int32NullParameter.Null);
                }
                else
                {
                    if (!int.TryParse(rawValue, out var value))
                    {
                        bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                        return false;
                    }
                    list.Add(value);
                }
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetInt32(this ModelBindingContext bindingContext, string name, ref bool hasValue, out int? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!int.TryParse(rawValue, out var value))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = value;
        }
        return true;
    }

    public static bool TryGetInt32List(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<int>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<int>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!int.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetInt64Null(this ModelBindingContext bindingContext, string name, ref bool hasValue, out long? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (rawValue == "null")
            {
                hasValue = true;
                result = Int64NullParameter.Null;
            }
            else
            {
                if (!long.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                hasValue = true;
                result = value;
            }
        }
        return true;
    }

    public static bool TryGetInt64NullList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<long>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<long>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (rawValue == "null")
                {
                    list.Add(Int64NullParameter.Null);
                }
                else
                {
                    if (!long.TryParse(rawValue, out var value))
                    {
                        bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                        return false;
                    }
                    list.Add(value);
                }
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetInt64(this ModelBindingContext bindingContext, string name, ref bool hasValue, out long? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!long.TryParse(rawValue, out var value))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = value;
        }
        return true;
    }

    public static bool TryGetInt64List(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<long>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<long>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!long.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetBigIntegerNull(this ModelBindingContext bindingContext, string name, ref bool hasValue, out BigInteger? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (rawValue == "null")
            {
                hasValue = true;
                result = BigIntegerNullParameter.Null;
            }
            else
            {
                if (!BigInteger.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                    return false;
                }
                hasValue = true;
                result = value;
            }
        }
        return true;
    }

    public static bool TryGetBigIntegerNullList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<BigInteger>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<BigInteger>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (rawValue == "null")
                {
                    list.Add(BigIntegerNullParameter.Null);
                }
                else
                {
                    if (!BigInteger.TryParse(rawValue, out var value))
                    {
                        bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                        return false;
                    }
                    list.Add(value);
                }
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetBigInteger(this ModelBindingContext bindingContext, string name, ref bool hasValue, out BigInteger? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!BigInteger.TryParse(rawValue, out var value))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = value;
        }
        return true;
    }

    public static bool TryGetBigIntegerList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<BigInteger>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<BigInteger>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!BigInteger.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetDateTime(this ModelBindingContext bindingContext, string name, ref bool hasValue, out DateTime? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!DateTimeOffset.TryParse(rawValue, out var value))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = value.UtcDateTime;
        }
        return true;
    }

    public static bool TryGetDateTimeList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<DateTime>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<DateTime>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!DateTimeOffset.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                    return false;
                }
                list.Add(value.UtcDateTime);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetStringNull(this ModelBindingContext bindingContext, string name, ref bool hasValue, out string? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (rawValue == "null")
            {
                hasValue = true;
                result = StringNullParameter.Null;
            }
            else
            {
                hasValue = true;
                result = Regexes.RestrictedUnicode().Replace(
                    rawValue.Length > 1 && rawValue[0] == '"' && rawValue[^1] == '"'
                        ? rawValue[1..^1]
                        : rawValue,
                    Regexes.NullEscapeString);
            }
        }
        return true;
    }

    public static bool TryGetStringNullList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<string>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<string>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (rawValue == "null")
                {
                    list.Add(StringNullParameter.Null);
                }
                else
                {
                    list.Add(Regexes.RestrictedUnicode().Replace(
                        rawValue.Length > 1 && rawValue[0] == '"' && rawValue[^1] == '"'
                            ? rawValue[1..^1]
                            : rawValue,
                        Regexes.NullEscapeString));
                }
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetString(this ModelBindingContext bindingContext, string name, ref bool hasValue, out string? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            hasValue = true;
            result = Regexes.RestrictedUnicode().Replace(rawValue, Regexes.NullEscapeString);
        }
        return true;
    }

    public static bool TryGetStringList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<string>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            hasValue = true;
            result = [.. rawValues.Select(x => Regexes.RestrictedUnicode().Replace(x, Regexes.NullEscapeString))];
        }
        return true;
    }

    public static bool TryGetString(this ModelBindingContext bindingContext, string name, [NotNullWhen(true)] out string? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            result = Regexes.RestrictedUnicode().Replace(rawValue, Regexes.NullEscapeString);
            return true;
        }
        bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
        return false;
    }

    public static bool TryGetJson(this ModelBindingContext bindingContext, string name, [NotNullWhen(true)] out string? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (rawValue == "null")
            {
                result = JsonParameter.Null;
                return true;
            }
            else
            {
                try
                {
                    var json = NormalizeJson(rawValue);
                    using var doc = JsonDocument.Parse(json);
                    result = json;
                    return true;
                }
                catch (JsonException) { }
            }
        }
        bindingContext.ModelState.TryAddModelError(name, "Invalid JSON value.");
        return false;
    }

    public static bool TryGetJsonArray(this ModelBindingContext bindingContext, string name, [NotNullWhen(true)] out string[]? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            try
            {
                if (rawValue.AsSpan().TrimStart() is [ '[', .. ] or [ '{', .. ])
                {
                    using var doc = JsonDocument.Parse(rawValue);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        bindingContext.ModelState.TryAddModelError(name, "Invalid JSON array.");
                        return false;
                    }
                    result = [.. doc.RootElement
                        .EnumerateArray()
                        .Select(x => x.GetRawText())
                        .Select(x => x == "null" ? JsonParameter.Null : NormalizeJson(x))];
                }
                else
                {
                    result = [.. rawValue
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x =>
                        {
                            if (x == "null") return JsonParameter.Null;
                            var json = NormalizeJson(x);
                            using var doc = JsonDocument.Parse(json);
                            return json;
                        })];
                }
                return true;
            }
            catch (JsonException) { }
        }
        bindingContext.ModelState.TryAddModelError(name, "Invalid JSON array.");
        return false;
    }

    static string NormalizeJson(string value)
    {
        switch (value[0])
        {
            case '{':
            case '[':
            case '"':
            case 't' when value == "true":
            case 'f' when value == "false":
            case 'n' when value == "null":
                return value;
            default:
                return $"\"{value}\"";
        }
    }

    public static bool TryGetSelectionFields(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<SelectionField>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<SelectionField>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!SelectionField.TryParse(rawValue, out var value))
                {
                    bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                    return false;
                }
                list.Add(value);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetUtf8Bytes(this ModelBindingContext bindingContext, string name, ref bool hasValue, out byte[]? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            if (!Utf8.TryParse(rawValue, out var bytes))
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid value.");
                return false;
            }
            hasValue = true;
            result = bytes;
        }
        return true;
    }

    public static bool TryGetUtf8BytesList(this ModelBindingContext bindingContext, string name, ref bool hasValue, out List<byte[]>? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameterList(name, out var rawValues))
        {
            var list = new List<byte[]>(rawValues.Length);
            foreach (var rawValue in rawValues)
            {
                if (!Utf8.TryParse(rawValue, out var bytes))
                {
                    bindingContext.ModelState.TryAddModelError(name, "List contains invalid value.");
                    return false;
                }
                list.Add(bytes);
            }
            hasValue = true;
            result = list;
        }
        return true;
    }

    public static bool TryGetMicheline(this ModelBindingContext bindingContext, string name, ref bool hasValue, out IMicheline? result)
    {
        result = null;
        if (bindingContext.TryGetQueryParameter(name, out var rawValue))
        {
            try
            {
                using var doc = JsonDocument.Parse(rawValue);
                result = Micheline.FromJson(doc.RootElement);
            }
            catch
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid Micheline JSON value.");
                return false;
            }
            if (result == null)
            {
                bindingContext.ModelState.TryAddModelError(name, "Invalid Micheline JSON value.");
                return false;
            }
            hasValue = true;
        }
        return true;
    }

    public static async Task<T?> BindChild<T>(
        this ModelBindingContext ctx,
        IModelMetadataProvider metadataProvider,
        IModelBinderFactory factory,
        string modelName) where T : class
    {
        var metadata = metadataProvider.GetMetadataForType(typeof(T));
        var binder = factory.CreateBinder(new ModelBinderFactoryContext
        {
            Metadata = metadata,
            CacheToken = metadata,
        });
        using (ctx.EnterNestedScope(metadata, modelName, modelName, null))
        {
            await binder.BindModelAsync(ctx);
            return ctx.Result.IsModelSet ? (T?)ctx.Result.Model : null;
        }
    }

    static bool TryGetQueryParameter(this ModelBindingContext bindingContext, string name, [NotNullWhen(true)] out string? value)
    {
        value = bindingContext.ActionContext.HttpContext.Request.Query[name].FirstOrDefault();
        return !string.IsNullOrEmpty(value);
    }

    static bool TryGetQueryParameterList(this ModelBindingContext bindingContext, string name, [NotNullWhen(true)] out string[]? values)
    {
        values = bindingContext.ActionContext.HttpContext.Request.Query[name]
            .FirstOrDefault()?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return values != null && values.Length != 0;
    }
}
