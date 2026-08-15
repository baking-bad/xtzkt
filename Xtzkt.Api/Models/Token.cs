using System.Numerics;
using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class Token
{
    /// <summary>Internal unique token id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the token belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Contract (token issuer).</summary>
    public required AddressInfo Contract { get; set; }

    /// <summary>Token id within the contract.</summary>
    public BigInteger TokenId { get; set; }

    /// <summary>Token standard (`fa1.2`, `fa2`, `erc20`, `erc721` or `erc1155`).</summary>
    public required string Standard { get; set; }

    /// <summary>Address that first minted the token.</summary>
    public required AddressInfo FirstMinter { get; set; }

    /// <summary>Level of the block where the token was first seen.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the token was first seen.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the token was last seen.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the token was last seen.</summary>
    public DateTime LastTimestamp { get; set; }

    /// <summary>Total number of transfers.</summary>
    public int TransfersCount { get; set; }

    /// <summary>Total number of balances ever created.</summary>
    public int BalancesCount { get; set; }

    /// <summary>Number of current holders (non-zero balances).</summary>
    public int HoldersCount { get; set; }

    /// <summary>Total amount minted.</summary>
    public BigInteger TotalMinted { get; set; }

    /// <summary>Total amount burned.</summary>
    public BigInteger TotalBurned { get; set; }

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

    /// <summary>Metadata resolution status (`pending`, `ok`, `failedToFetch`, `failedToDecode`, `sizeLimitExceeded`, `depthLimitExceeded`, `invalidJson`, `invalidUri`).</summary>
    public required string MetadataStatus { get; set; }

    /// <summary>External metadata link (ipfs/http) being resolved, if any.</summary>
    public string? MetadataLink { get; set; }

    /// <summary>Time when the metadata was last synced (resolved, or the last resolve attempt), if any.</summary>
    public DateTime? MetadataSyncedAt { get; set; }
}
