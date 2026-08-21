using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Abstract;

namespace Xtzkt.Api.Models;

public class BridgeTicketTransfer : IOpgActivity
{
    /// <summary>Internal unique bridge ticket transfer id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the transfer belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Level of the block where the transfer happened.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block where the transfer happened.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Bridge ticket being transferred.</summary>
    public required BridgeTicketInfo Ticket { get; set; }

    /// <summary>Sender address (`null` for bridging in from L1).</summary>
    public AddressInfo? From { get; set; }

    /// <summary>Target address (`null` for withdrawing back to L1).</summary>
    public AddressInfo? To { get; set; }

    /// <summary>
    /// Amount credited or debited, in ticket units — unlike EVM values, it is not scaled
    /// by 18 decimals. It's a string only because a ticket amount has no upper bound.
    /// </summary>
    public BigInteger Amount { get; set; }

    /// <summary>Id of the transaction operation, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? TransactionId { get; set; }

    /// <summary>Id of the deposit operation, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? DepositId { get; set; }
}
