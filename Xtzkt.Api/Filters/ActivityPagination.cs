using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;
using Xtzkt.Api.Utils.Validation;

namespace Xtzkt.Api.Filters;

public class ActivityPagination : INormalizable
{
    /// <summary>
    /// Comma-separated list of fields (with optional sort direction) to sort by.
    ///
    /// Examples: `?sort=id`, `?sort=id.desc`, `?sort=balance.desc,id.asc`.
    /// </summary>
    public SortParameter? Sort { get; set; }

    /// <summary>
    /// Sort field values of the last item from the previous page — one value per `sort` field, comma-separated.
    /// Returns the items that go after it. This is the only way to page here, and it stays fast
    /// no matter how deep into the list you go.
    ///
    /// Examples: `?cursor=1234` with `?sort=id`, `?cursor=1000,1234` with `?sort=balance.desc,id`.
    /// </summary>
    public CursorParameter? Cursor { get; set; }

    /// <summary>
    /// Maximum number of items to return (1-{MaxActivityLimit}). Lower than elsewhere, because activity is merged
    /// from several tables per request, so its cost grows faster.
    ///
    /// Example: `?limit=50`.
    /// </summary>
    [ActivityLimitRange]
    public int Limit { get; set; } = ApiConfig.DefaultLimit;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.sort", Sort),
        ($"{name}.cursor", Cursor),
        ($"{name}.limit", Limit));
}
