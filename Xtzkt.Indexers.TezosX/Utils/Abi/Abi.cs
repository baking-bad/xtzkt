using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils.Crypto;

namespace Xtzkt.Indexers.TezosX.Utils.Abi;

public class Abi(List<AbiItem> items)
{
    static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public IReadOnlyList<AbiItem> Items { get; init; } = items;

    readonly IReadOnlyList<FunctionAbi> Functions = [..items
        .Where(x => x is FunctionAbi)
        .OfType<FunctionAbi>()];

    readonly IReadOnlyList<EventAbi> Events = [..items
        .Where(x => x is EventAbi)
        .OfType<EventAbi>()];

    public bool TryGetFunction(ReadOnlySpan<byte> input, [NotNullWhen(true)] out FunctionAbi? function)
    {
        if (input.Length >= 4)
        {
            foreach (var fn in Functions)
            {
                var selector = fn.SelectorBytes;
                if (selector[0] == input[0] && selector[1] == input[1] && selector[2] == input[2] && selector[3] == input[3])
                {
                    function = fn;
                    return true;
                }
            }
        }

        function = null;
        return false;
    }

    public bool TryGetEvent(byte[] topic, [NotNullWhen(true)] out EventAbi? @event)
    {
        foreach (var e in Events)
        {
            if (e.TopicBytes.IsEqual(topic))
            {
                @event = e;
                return true;
            }
        }

        @event = null;
        return false;
    }

    public bool TryGetEvent(string topic, [NotNullWhen(true)] out EventAbi? @event)
    {
        foreach (var e in Events)
        {
            if (e.Topic == topic)
            {
                @event = e;
                return true;
            }
        }

        @event = null;
        return false;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(Items, SerializerOptions);
    }

    public static Abi FromJson(string json)
    {
        var items = JsonSerializer.Deserialize<List<AbiItem>>(json, SerializerOptions)
            ?? throw new Exception("Invalid ABI JSON");

        return new(items);
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConstructorAbi), "constructor")]
[JsonDerivedType(typeof(FunctionAbi), "function")]
[JsonDerivedType(typeof(ReceiveAbi), "receive")]
[JsonDerivedType(typeof(FallbackAbi), "fallback")]
[JsonDerivedType(typeof(EventAbi), "event")]
[JsonDerivedType(typeof(ErrorAbi), "error")]
public abstract class AbiItem { }

public sealed class ConstructorAbi : AbiItem
{
    [JsonPropertyName("inputs")]
    public IReadOnlyList<AbiParameter> Inputs { get; init; } = [];

    [JsonPropertyName("stateMutability")]
    public StateMutability StateMutability { get; init; } = StateMutability.NonPayable;
}

public sealed class FunctionAbi : AbiItem
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("inputs")]
    public IReadOnlyList<AbiParameter> Inputs { get; init; } = [];

    [JsonPropertyName("outputs")]
    public IReadOnlyList<AbiParameter> Outputs { get; init; } = [];

    [JsonPropertyName("stateMutability")]
    public StateMutability StateMutability { get; init; } = StateMutability.NonPayable;

    [JsonIgnore]
    public string Signature => _Signature ??= $"{Name}({string.Join(',', Inputs.Select(x => x.GetCanonicalType()))})";
    string? _Signature = null;

    [JsonIgnore]
    public string Selector => Keccak256.GetHash(Encoding.UTF8.GetBytes(Signature), 4);

    [JsonIgnore]
    public byte[] SelectorBytes => _SelectorBytes ??= Keccak256.GetHashBytes(Encoding.UTF8.GetBytes(Signature))[..4];
    byte[]? _SelectorBytes = null;
}

public sealed class ReceiveAbi : AbiItem
{
    [JsonPropertyName("stateMutability")]
    public StateMutability StateMutability { get; init; } = StateMutability.Payable;
}

public sealed class FallbackAbi : AbiItem
{
    [JsonPropertyName("stateMutability")]
    public StateMutability StateMutability { get; init; } = StateMutability.NonPayable;
}

public sealed class EventAbi : AbiItem
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("inputs")]
    public IReadOnlyList<AbiParameter> Inputs { get; init; } = [];

    [JsonPropertyName("anonymous")]
    public bool Anonymous { get; init; }

    [JsonIgnore]
    public string Signature => _Signature ??= $"{Name}({string.Join(',', Inputs.Select(x => x.GetCanonicalType()))})";
    string? _Signature = null;

    [JsonIgnore]
    public string Topic => Keccak256.GetHash(Encoding.UTF8.GetBytes(Signature));

    [JsonIgnore]
    public byte[] TopicBytes => _TopicBytes ??= Keccak256.GetHashBytes(Encoding.UTF8.GetBytes(Signature));
    byte[]? _TopicBytes = null;

    public string DecodeToJson(byte[][] topics, byte[] data)
    {
        var obj = AbiDecoder.Decode(data, [..Inputs.Where(x => x.Indexed != true)]);
        var i = 0;
        foreach (var input in Inputs.Where(x => x.Indexed == true))
        {
            var paramName = input.Name ?? $"@{i}";
            if (obj.ContainsKey(paramName))
            {
                var ind = 0;
                do { paramName = $"{paramName}{++ind}"; }
                while (obj.ContainsKey(paramName));
            }
            obj.Add(paramName, AbiDecoder.DecodeTopic(topics[++i], input));
        }
        return AbiDecoder.SerializeToJson(obj);
    }
}

public sealed class ErrorAbi : AbiItem
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("inputs")]
    public IReadOnlyList<AbiParameter> Inputs { get; init; } = [];

    [JsonIgnore]
    public string Signature => $"{Name}({string.Join(',', Inputs.Select(x => x.GetCanonicalType()))})";

    [JsonIgnore]
    public string Selector => Keccak256.GetHash(Encoding.UTF8.GetBytes(Signature), 4);
}

public sealed class AbiParameter
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("internalType")]
    public string? InternalType { get; init; }

    [JsonPropertyName("components")]
    public IReadOnlyList<AbiParameter>? Components { get; init; }

    [JsonPropertyName("indexed")]
    public bool? Indexed { get; init; }

    public string GetCanonicalType()
    {
        return Components is { Count: > 0 } c && Type.StartsWith("tuple")
            ? $"({string.Join(',', c.Select(x => x.GetCanonicalType()))}){Type[5..]}"
            : Type;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StateMutability
{
    [JsonStringEnumMemberName("pure")]
    Pure,
    [JsonStringEnumMemberName("view")]
    View,
    [JsonStringEnumMemberName("nonpayable")]
    NonPayable,
    [JsonStringEnumMemberName("payable")]
    Payable,
}