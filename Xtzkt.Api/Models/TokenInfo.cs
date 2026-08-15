using System.Numerics;
using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class TokenInfo
{
    /// <summary>Internal unique token id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Contract (token issuer).</summary>
    public required AddressInfo Contract { get; set; }

    /// <summary>Token id within the contract.</summary>
    public BigInteger TokenId { get; set; }

    /// <summary>Token standard (`fa1.2`, `fa2`, `erc20`, `erc721` or `erc1155`).</summary>
    public required string Standard { get; set; }

    /// <summary>Total supply.</summary>
    public BigInteger TotalSupply { get; set; }

    /// <summary>Token name (from metadata).</summary>
    public string? Name { get; set; }

    /// <summary>Token symbol (from metadata).</summary>
    public string? Symbol { get; set; }

    /// <summary>Token decimals (from metadata).</summary>
    public int? Decimals { get; set; }

    /// <summary>Token metadata.</summary>
    public RawJson? Metadata { get; set; }
}
