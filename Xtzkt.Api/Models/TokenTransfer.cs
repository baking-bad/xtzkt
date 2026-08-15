using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Abstract;

namespace Xtzkt.Api.Models;

public class TokenTransfer : IOpgActivity
{
    /// <summary>Internal unique token transfer id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the transfer belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Level of the block where the transfer happened.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block where the transfer happened.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Token being transferred.</summary>
    public required TokenInfo Token { get; set; }

    /// <summary>Sender address (`null` for minting).</summary>
    public AddressInfo? From { get; set; }

    /// <summary>Entrypoint via which the tokens were sent.</summary>
    [JsonConverter(typeof(Utf8Converter))]
    public byte[]? FromEntrypoint { get; set; }

    /// <summary>Target address (`null` for burning).</summary>
    public AddressInfo? To { get; set; }

    /// <summary>Entrypoint via which the tokens were received.</summary>
    [JsonConverter(typeof(Utf8Converter))]
    public byte[]? ToEntrypoint { get; set; }

    /// <summary>Amount of tokens transferred.</summary>
    public BigInteger Amount { get; set; }

    /// <summary>Id of the transaction operation, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? TransactionId { get; set; }

    /// <summary>Id of the origination operation, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? OriginationId { get; set; }

    /// <summary>Id of the migration, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? MigrationId { get; set; }
}
