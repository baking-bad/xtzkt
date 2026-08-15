using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models.Search;

/// <summary>
/// An operation as a whole, identified by its hash. A hash may cover several operations
/// of several kinds (an operation group), which is why there's no internal id here: the hash
/// is the identity, and the contents are to be fetched by it.
/// </summary>
public class OperationSearchResult : SearchResult
{
    [JsonIgnore]
    public override int Priority => 2;

    /// <summary>Level of the block, in which the operation was included.</summary>
    public int Level { get; init; }

    /// <summary>Timestamp of the block, in which the operation was included.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Operation hash.</summary>
    public required string Hash { get; init; }
}
