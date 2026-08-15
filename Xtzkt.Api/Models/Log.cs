using System.Text.Json.Serialization;
using Netezos.Encoding;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "runtime")]
[JsonDerivedType(typeof(EvmLog), Runtimes.Evm)]
[JsonDerivedType(typeof(MichelsonLog), Runtimes.Michelson)]
public abstract class Log
{
    /// <summary>
    /// Internal unique log id.
    /// </summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>
    /// Chain the log belongs to.
    /// </summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>
    /// Level of the block where the log was emitted.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Timestamp of the block where the log was emitted.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Address that emitted the log. Usually a contract, but for `evm` runtime it can also be
    /// a non-contract address with an EIP-7702 delegation, executing the code of its delegate.
    /// </summary>
    public required AddressInfo Address { get; set; }

    /// <summary>
    /// 32-bit hash of the parameter and storage types of the contract, whose code was executed
    /// when the log was emitted. For an address with an EIP-7702 delegation that's the delegate,
    /// not the address itself.
    /// </summary>
    public int ContractTypeHash { get; set; }

    /// <summary>
    /// 32-bit hash of the code of the contract, whose code was executed when the log was emitted.
    /// For an address with an EIP-7702 delegation that's the delegate, not the address itself.
    /// </summary>
    public int ContractCodeHash { get; set; }

    /// <summary>
    /// Event name (if the event signature is known).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Log payload in JSON format (`null` if couldn't decode).
    /// </summary>
    public RawJson? Payload { get; set; }

    /// <summary>
    /// Which source the name and the payload were decoded with:
    /// `false` for a trusted one (contract ABI for `evm`, event type for `michelson`),
    /// `true` if the only available source was a guess, made by matching the event signature hash
    /// against popular standards, because the contract ABI is unknown,
    /// `null` if there was no source to decode them with at all.
    /// Note, the payload may be `null` even when the source is known, if it failed to decode.
    /// </summary>
    public bool? Guessed { get; set; }
}

public class EvmLog : Log
{
    /// <summary>
    /// Id of the transaction operation, emitted the log (if any).
    /// </summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? TransactionId { get; set; }

    /// <summary>
    /// Id of the origination operation, emitted the log (if any).
    /// </summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? OriginationId { get; set; }

    /// <summary>
    /// Indexed event topics, where the first one is usually the event signature hash.
    /// </summary>
    [JsonConverter(typeof(HexListConverter))]
    public required List<byte[]> Topics { get; set; }

    /// <summary>
    /// Non-indexed event data.
    /// </summary>
    [JsonConverter(typeof(HexConverter))]
    public required byte[] Data { get; set; }
}

public class MichelsonLog : Log
{
    /// <summary>
    /// Id of the transaction operation, emitted the log.
    /// </summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long TransactionId { get; set; }

    /// <summary>
    /// Event payload type in Micheline format.
    /// </summary>
    public IMicheline? Type { get; set; }

    /// <summary>
    /// Event payload in Micheline format.
    /// </summary>
    public IMicheline? RawPayload { get; set; }
}
