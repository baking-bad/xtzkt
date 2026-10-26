using System.Text.Json.Serialization;

namespace Xtzkt.Services.Admin.Models;

public class AssetInfo
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public string? Description { get; set; }
    public string? Logo { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required long[]? Tokens { get; set; }
}
