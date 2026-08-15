using System.ComponentModel.DataAnnotations;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class SearchFilter : INormalizable
{
    /// <summary>
    /// Search query. The search mode is derived from its value:
    /// a known hash (address, block or operation) searches the corresponding entities by hash,
    /// a number searches blocks by level, and any other string searches addresses by alias
    /// and tokens by name/symbol (fuzzy, tolerant to typos).
    ///
    /// Examples: `?query=tz1KqTpEZ7Yob7QbPE4Hy4Wo8fHG8LhKxZSx`, `?query=1500000`, `?query=tezos domains`.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(128, MinimumLength = 1)]
    public string Query { get; set; } = null!;

    /// <summary>
    /// Restricts the search to particular chains.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainInfoParameter? Chain { get; set; }

    /// <summary>
    /// Comma-separated list of scopes to search in (`address`, `block`, `operation`, `token`).
    /// If not specified, all scopes are used.
    ///
    /// Examples: `?scopes=address`, `?scopes=address,token`.
    /// </summary>
    public SearchScopesParameter? Scopes { get; set; }

    /// <summary>
    /// Maximum number of items to return in total (1-100), best matches first,
    /// no matter how many scopes were searched.
    ///
    /// Example: `?limit=20`.
    /// </summary>
    [Range(1, 100)]
    public int Limit { get; set; } = 10;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        // unlike every other parameter, the query is arbitrary text, so it has to be escaped:
        // an unescaped '&' or '=' in it would let one query produce another one's cache key
        ($"{name}.query", Uri.EscapeDataString(Query)),
        ($"{name}.chain", Chain),
        ($"{name}.scopes", Scopes),
        ($"{name}.limit", Limit));
}
