using System.ComponentModel.DataAnnotations;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class Pagination : INormalizable
{
    /// <summary>
    /// Comma-separated list of fields (with optional sort direction) to sort by.
    ///
    /// Examples: `?sort=id`, `?sort=id.desc`, `?sort=balance.desc,id.asc`.
    /// </summary>
    public SortParameter? Sort { get; set; }

    /// <summary>
    /// Sort field values of the last item from the previous page — one value per `sort` field, comma-separated.
    /// Returns the items that go after it. Faster and safer than `offset` on long lists, because it doesn't
    /// skip or duplicate items when the data changes between requests.
    ///
    /// Examples: `?cursor=1234` with `?sort=id`, `?cursor=1000,1234` with `?sort=balance.desc,id`.
    /// </summary>
    public CursorParameter? Cursor { get; set; }

    /// <summary>
    /// Number of items to skip. Simple, but gets slower the deeper you go — prefer `cursor` for long lists.
    ///
    /// Example: `?offset=100`.
    /// </summary>
    public int Offset { get; set; } = 0;

    /// <summary>
    /// Maximum number of items to return (1-10000).
    ///
    /// Example: `?limit=50`.
    /// </summary>
    [Range(1, 10000)]
    public int Limit { get; set; } = 100;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.sort", Sort),
        ($"{name}.cursor", Cursor),
        ($"{name}.offset", Offset),
        ($"{name}.limit", Limit));
}
