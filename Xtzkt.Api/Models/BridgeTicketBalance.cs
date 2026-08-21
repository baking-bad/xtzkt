using System.Numerics;
using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class BridgeTicketBalance
{
    /// <summary>Internal unique bridge ticket balance id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the balance belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Address holding the balance.</summary>
    public required AddressInfo Address { get; set; }

    /// <summary>Bridge ticket the balance is for.</summary>
    public required BridgeTicketInfo Ticket { get; set; }

    /// <summary>
    /// Balance amount, in ticket units — unlike EVM values, it is not scaled by 18 decimals.
    /// It's a string only because a ticket amount has no upper bound.
    /// </summary>
    public BigInteger Balance { get; set; }

    /// <summary>Level of the block where the balance was first seen.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the balance was first seen.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the balance was last updated.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the balance was last updated.</summary>
    public DateTime LastTimestamp { get; set; }

    /// <summary>Total number of transfers.</summary>
    public int TransfersCount { get; set; }
}
