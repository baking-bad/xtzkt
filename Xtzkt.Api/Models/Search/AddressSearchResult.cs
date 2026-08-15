using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models.Search;

public class AddressSearchResult : SearchResult
{
    [JsonIgnore]
    public override int Priority => 0;

    /// <summary>Address hash.</summary>
    public required string Hash { get; init; }

    /// <summary>Address type.</summary>
    public required string Type { get; init; }

    /// <summary>Human-readable name of the address, if known.</summary>
    public string? Alias { get; init; }
}
