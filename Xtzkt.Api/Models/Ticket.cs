using System.Numerics;
using System.Text.Json.Serialization;
using Netezos.Encoding;

namespace Xtzkt.Api.Models;

public class Ticket
{
    /// <summary>Internal unique ticket id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the ticket belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Contract (ticketer) that issued the ticket.</summary>
    public required AddressInfo Ticketer { get; set; }

    /// <summary>Address that first minted the ticket.</summary>
    public required AddressInfo FirstMinter { get; set; }

    /// <summary>Level of the block where the ticket was first seen.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the ticket was first seen.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the ticket was last seen.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the ticket was last seen.</summary>
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

    /// <summary>
    /// Canonical hash of the ticket: `keccak256` of the ticketer address and the ticket content,
    /// both in their binary forms — the same hash bridged tickets are identified by on Tezos X,
    /// so it can be used to match a ticket across the layers.
    ///
    /// Note, the content type is not hashed, so tickets of the same ticketer whose types encode
    /// their content identically (`nat` and `int`, `bytes` and `address`, ...) share the hash.
    /// </summary>
    [JsonConverter(typeof(HexConverter))]
    public required byte[] WeakHash { get; set; }

    /// <summary>Ticket content type in Micheline format.</summary>
    public required IMicheline RawType { get; set; }

    /// <summary>Ticket content in Micheline format.</summary>
    public required IMicheline RawContent { get; set; }

    /// <summary>Ticket content in JSON format.</summary>
    public RawJson? Content { get; set; }
}
