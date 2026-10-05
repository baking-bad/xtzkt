using System.Text.Json;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Utils;

public static class Micheline
{
    public static JsonElement? GetCombElement(JsonElement value, int index)
    {
        while (index >= 0)
        {
            if (GetCombArgs(value) is not JsonElement args)
                return index == 0 ? value : null;

            var count = args.GetArrayLength();
            if (count < 2)
                return null;

            if (index < count - 1)
                return args[index];

            index -= count - 1;
            value = args[count - 1];
        }
        return null;
    }

    public static string? GetCombInt(JsonElement value, int index)
    {
        return GetCombElement(value, index) is JsonElement el &&
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty("int", out var res) &&
            res.ValueKind == JsonValueKind.String
                ? res.GetString()
                : null;
    }

    public static string? GetCombString(JsonElement value, int index)
    {
        return GetCombElement(value, index) is JsonElement el &&
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty("string", out var res) &&
            res.ValueKind == JsonValueKind.String
                ? res.GetString()
                : null;
    }

    public static byte[]? GetCombBytes(JsonElement value, int index)
    {
        return GetCombElement(value, index) is JsonElement el &&
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty("bytes", out var res) &&
            res.ValueKind == JsonValueKind.String
                ? Hex.GetBytes(res.GetString()!)
                : null;
    }

    static JsonElement? GetCombArgs(JsonElement value)
    {
        // a comb can be serialized as a sequence, as a nested prim or as a flattened prim
        if (value.ValueKind == JsonValueKind.Array)
            return value;

        if (value.ValueKind != JsonValueKind.Object)
            return null;

        if (!value.TryGetProperty("prim", out var prim) ||
            prim.ValueKind != JsonValueKind.String ||
            !prim.ValueEquals("Pair"))
            return null;

        return value.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array
            ? args
            : null;
    }
}
