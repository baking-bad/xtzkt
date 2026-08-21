using System.Numerics;
using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class BridgeTicket
{
    /// <summary>Internal unique bridge ticket id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the bridge ticket belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>
    /// Hash of the L1 ticket behind the bridged asset: `keccak256` of the ticketer address and the
    /// ticket content, both in their binary forms. It's all the bridge keeps on the chain — the
    /// ticketer and the content themselves stay on L1, and the same hash on a `/v1/tickets` entry
    /// (`weakHash`) is what links the two.
    ///
    /// The content type is not hashed, so this is a lookup key rather than an identity: L1 tickets
    /// of the same ticketer whose types encode their content identically share it.
    /// </summary>
    [JsonConverter(typeof(HexConverter))]
    public required byte[] WeakHash { get; set; }

    /// <summary>Level of the block where the bridge ticket was first seen.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the bridge ticket was first seen.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the bridge ticket was last seen.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the bridge ticket was last seen.</summary>
    public DateTime LastTimestamp { get; set; }

    /// <summary>Total number of transfers.</summary>
    public int TransfersCount { get; set; }

    /// <summary>Total number of balances ever created.</summary>
    public int BalancesCount { get; set; }

    /// <summary>Number of current holders (non-zero balances).</summary>
    public int HoldersCount { get; set; }

    /// <summary>Total amount bridged in from L1 (ticket units, not scaled).</summary>
    public BigInteger TotalMinted { get; set; }

    /// <summary>Total amount withdrawn back to L1 (ticket units, not scaled).</summary>
    public BigInteger TotalBurned { get; set; }

    /// <summary>Total amount currently on the chain (ticket units, not scaled).</summary>
    public BigInteger TotalSupply { get; set; }
}
