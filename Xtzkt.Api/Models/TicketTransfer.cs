using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Abstract;

namespace Xtzkt.Api.Models;

public class TicketTransfer : IOpgActivity
{
    /// <summary>Internal unique ticket transfer id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the transfer belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Level of the block where the transfer happened.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block where the transfer happened.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Ticket being transferred.</summary>
    public required TicketInfo Ticket { get; set; }

    /// <summary>Sender address (`null` for minting).</summary>
    public AddressInfo? From { get; set; }

    /// <summary>Target address (`null` for burning).</summary>
    public AddressInfo? To { get; set; }

    /// <summary>Amount of tickets transferred.</summary>
    public BigInteger Amount { get; set; }

    /// <summary>Id of the transaction operation, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? TransactionId { get; set; }

    /// <summary>Id of the transfer_ticket operation, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? TransferTicketId { get; set; }

    /// <summary>Id of the smart_rollup_execute operation, caused the transfer (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? SmartRollupExecuteId { get; set; }
}
