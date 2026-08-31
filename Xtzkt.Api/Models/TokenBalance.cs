using System.Numerics;
using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class TokenBalance
{
    /// <summary>Internal unique token balance id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the balance belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Address holding the balance.</summary>
    public required AddressInfo Address { get; set; }

    /// <summary>Token the balance is for.</summary>
    public required TokenInfo Token { get; set; }

    /// <summary>Balance amount.</summary>
    public BigInteger Balance { get; set; }

    /// <summary>Entrypoint used to receive the ticket (tickets only).</summary>
    [JsonConverter(typeof(Utf8Converter))]
    public byte[]? Entrypoint { get; set; }

    /// <summary>Level of the block where the balance was first seen.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the balance was first seen.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the balance was last updated.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the balance was last updated.</summary>
    public DateTime LastTimestamp { get; set; }

    /// <summary>Total number of transfers.</summary>
    public long TransfersCount { get; set; }
}
