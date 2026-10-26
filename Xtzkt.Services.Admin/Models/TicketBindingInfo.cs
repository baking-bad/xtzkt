using System.Text.Json.Serialization;

namespace Xtzkt.Services.Admin.Models;

public class TicketBindingInfo
{
    public required string TicketHash { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required long? TokenId { get; set; }
}
