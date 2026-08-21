using System.Text.Json.Serialization;
using Netezos.Encoding;

namespace Xtzkt.Api.Models;

public class TicketInfo
{
    /// <summary>Internal unique ticket id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Contract (ticketer) that issued the ticket.</summary>
    public required AddressInfo Ticketer { get; set; }

    /// <summary>Ticket content type in Micheline format.</summary>
    public required IMicheline RawType { get; set; }

    /// <summary>Ticket content in Micheline format.</summary>
    public required IMicheline RawContent { get; set; }

    /// <summary>Ticket content in JSON format.</summary>
    public RawJson? Content { get; set; }

    /// <summary>
    /// Canonical hash of the ticket: `keccak256` of the ticketer address and the ticket content,
    /// both in their binary forms (the content type is not hashed).
    /// </summary>
    [JsonConverter(typeof(HexConverter))]
    public required byte[] WeakHash { get; set; }
}
