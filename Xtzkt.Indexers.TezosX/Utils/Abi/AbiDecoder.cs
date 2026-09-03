using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Utils;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Utils.Abi;

public static class AbiDecoder
{
    const int WordSize = 32;

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWriting,
        Converters = { new BigIntegerStringConverter() },
    };

    public static string DecodeToJson(ReadOnlySpan<byte> data, IReadOnlyList<AbiParameter> abi)
    {
        return abi.Count > 0
            ? SerializeToJson(Decode(data, abi))
            : "{}";
    }

    public static string SerializeToJson(Dictionary<string, object> values)
    {
        return Regexes.RestrictedUnicode().Replace(
            JsonSerializer.Serialize(values, SerializerOptions),
            Regexes.NullEscapeString);
    }

    public static object DecodeTopic(byte[] topic, AbiParameter param)
    {
        var type = param.Type;

        if (topic.Length == WordSize &&
            type is not ("string" or "bytes") &&
            !type.EndsWith(']') &&
            !type.StartsWith("tuple"))
        {
            if (type is "address" or "bool" ||
                type.StartsWith("uint") ||
                type.StartsWith("int") ||
                type.StartsWith("bytes"))
                return DecodeScalar(topic, 0, type);
        }

        // hashed, or unsupported type
        return Hex.GetString(topic);
    }

    public static Dictionary<string, object> Decode(ReadOnlySpan<byte> data, IReadOnlyList<AbiParameter> abi)
    {
        var result = new Dictionary<string, object>(abi.Count);
        var pos = 0;
        for (var i = 0; i < abi.Count; i++)
        {
            var param = abi[i];
            var paramName = param.Name ?? $"${i}";
            if (result.ContainsKey(paramName))
            {
                var ind = 0;
                do { paramName = $"{paramName}{++ind}"; }
                while (result.ContainsKey(paramName));
            }

            if (IsDynamic(param))
            {
                var offset = ReadInt32Checked(data, pos);
                result.Add(paramName, DecodeValue(data, offset, param));
                pos += WordSize;
            }
            else
            {
                result.Add(paramName, DecodeValue(data, pos, param));
                pos += GetSize(param);
            }
        }
        return result;
    }

    static bool IsDynamic(AbiParameter param)
    {
        var type = param.Type;
        if (type is "string" or "bytes")
            return true;

        var ind = type.LastIndexOf('[');
        if (ind >= 0)
        {
            var suffix = type[ind..];
            if (suffix == "[]")
                return true;

            return IsDynamic(new AbiParameter
            {
                Type = type[..ind],
                Components = param.Components,
            });
        }

        if (type.StartsWith("tuple") && param.Components is { Count: > 0 } components)
            return components.Any(IsDynamic);

        return false;
    }

    static int GetSize(AbiParameter param)
    {
        var type = param.Type;

        var ind = type.LastIndexOf('[');
        if (ind >= 0)
        {
            int count = int.Parse(type[(ind + 1)..^1]);
            return count * GetSize(new AbiParameter
            {
                Type = type[..ind],
                Components = param.Components,
            });
        }
        
        if (type.StartsWith("tuple") && param.Components is { Count: > 0 } components)
            return components.Sum(GetSize);
        
        return WordSize;
    }

    static object DecodeValue(ReadOnlySpan<byte> data, int offset, AbiParameter param)
    {
        var type = param.Type;

        var ind = type.LastIndexOf('[');
        if (ind >= 0)
        {
            var suffix = type[ind..];
            var item = new AbiParameter
            {
                Type = type[..ind],
                Components = param.Components,
            };

            if (suffix == "[]")
            {
                var cnt = ReadInt32Checked(data, offset);
                return DecodeArray(data, offset + WordSize, cnt, item);
            }
            else
            {
                var cnt = int.Parse(suffix[1..^1]);
                return DecodeArray(data, offset, cnt, item);
            }
        }

        if (type.StartsWith("tuple") && param.Components is { Count: > 0 } components)
            return Decode(data[offset..], components);

        return DecodeScalar(data, offset, type);
    }

    static object[] DecodeArray(ReadOnlySpan<byte> data, int offset, int count, AbiParameter item)
    {
        var res = new object[count];
        var bytes = data[offset..];

        if (IsDynamic(item))
        {
            for (int i = 0; i < count; i++)
            {
                var itemOffset = ReadInt32Checked(bytes, i * WordSize);
                res[i] = DecodeValue(bytes, itemOffset, item);
            }
        }
        else
        {
            var itemSize = GetSize(item);
            for (int i = 0; i < count; i++)
                res[i] = DecodeValue(bytes, i * itemSize, item);
        }

        return res;
    }

    static object DecodeScalar(ReadOnlySpan<byte> data, int offset, string type)
    {
        if (type == "bool")
            return data[offset + 31] != 0;

        if (type == "address")
            return Hex.GetString(data.Slice(offset + 12, 20));

        if (type == "string")
        {
            var len = ReadInt32Checked(data, offset);
            return Encoding.UTF8.GetString(data.Slice(offset + WordSize, len));
        }

        if (type == "bytes")
        {
            var len = ReadInt32Checked(data, offset);
            return Hex.GetString(data.Slice(offset + WordSize, len));
        }

        // bytesN
        if (type.StartsWith("bytes"))
        {
            var n = int.Parse(type[5..]);
            return Hex.GetString(data.Slice(offset, n));
        }

        // uintN
        if (type.StartsWith("uint"))
            return new BigInteger(data.Slice(offset, WordSize), true, true);

        // intN
        if (type.StartsWith("int"))
            return new BigInteger(data.Slice(offset, WordSize), false, true);

        throw new NotSupportedException($"Unsupported ABI scalar type: {type}");
    }

    static int ReadInt32Checked(ReadOnlySpan<byte> data, int offset)
    {
        var bytes = data.Slice(offset, 32);

        var pos = 0;
        while (bytes[pos] == 0 && pos < 31)
            pos++;

        if (pos < 28 || pos == 28 && bytes[28] >= 128)
            throw new OverflowException("uint256 value is too large for offset/length");

        int res = bytes[pos];
        for (var i = pos + 1; i < bytes.Length; i++)
            res = (res << 8) | bytes[i];
        
        return res;
    }

    public static string ReadString(ReadOnlySpan<byte> data, int offset)
    {
        var len = ReadInt32Checked(data, offset);
        return Encoding.UTF8.GetString(data.Slice(offset + WordSize, len));
    }
}