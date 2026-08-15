using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models.Search;

public class BlockSearchResult : SearchResult
{
    [JsonIgnore]
    public override int Priority => 3;

    /// <summary>Block level.</summary>
    public int Level { get; init; }

    /// <summary>Block timestamp.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Block hash.</summary>
    public required string Hash { get; init; }

    /// <summary>Block michelson hash.</summary>
    public string? MichelsonHash { get; init; }
}
