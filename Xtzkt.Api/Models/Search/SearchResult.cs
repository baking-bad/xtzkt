using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models.Search;

/// <summary>
/// An entity found by the search endpoint, carrying its identity data.
/// The actual type is told by the `scope` discriminator, which is also the scope
/// the entity was found in.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "scope")]
[JsonDerivedType(typeof(AddressSearchResult), SearchScopes.Address)]
[JsonDerivedType(typeof(BlockSearchResult), SearchScopes.Block)]
[JsonDerivedType(typeof(OperationSearchResult), SearchScopes.Operation)]
[JsonDerivedType(typeof(TokenSearchResult), SearchScopes.Token)]
public abstract class SearchResult
{
    /// <summary>Prioity to sort same score entities by.</summary>
    [JsonIgnore]
    public abstract int Priority { get; }

    /// <summary>Chain the entity belongs to.</summary>
    public required ChainInfo Chain { get; init; }
}
