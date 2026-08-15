using System.Numerics;
using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models.Search;

public class TokenSearchResult : SearchResult
{
    [JsonIgnore]
    public override int Priority => 1;

    /// <summary>Internal unique token id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; init; }

    /// <summary>Contract (token issuer).</summary>
    public required AddressInfo Contract { get; init; }

    /// <summary>Token id within the contract.</summary>
    public BigInteger TokenId { get; init; }

    /// <summary>Token standard (`fa1.2`, `fa2`, `erc20`, `erc721` or `erc1155`).</summary>
    public required string Standard { get; init; }

    /// <summary>Token name (from metadata).</summary>
    public string? Name { get; init; }

    /// <summary>Token symbol (from metadata).</summary>
    public string? Symbol { get; init; }

    /// <summary>Token decimals (from metadata).</summary>
    public int? Decimals { get; init; }
}
