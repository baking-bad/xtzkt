using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class BridgeTicketInfo
{
    /// <summary>Internal unique bridge ticket id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>
    /// Hash identifying the L1 ticket behind the bridged asset:
    /// `keccak256` of the ticketer address and the ticket content, both in their binary forms.
    /// </summary>
    [JsonConverter(typeof(HexConverter))]
    public required byte[] WeakHash { get; set; }
}
