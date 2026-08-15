using System.Numerics;
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

    /// <summary>32-bit hash of the ticket content type.</summary>
    public int TypeHash { get; set; }

    /// <summary>32-bit hash of the ticket content.</summary>
    public int ContentHash { get; set; }

    /// <summary>Total supply.</summary>
    public BigInteger TotalSupply { get; set; }
}
