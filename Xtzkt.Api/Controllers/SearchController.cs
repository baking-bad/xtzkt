using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Search;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers;

[ApiController]
[Tags("Search")]
[Route("v1/search")]
[Produces("application/json")]
public class SearchController(SearchRepository _search, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Search entities
    /// </summary>
    /// <remarks>
    /// Returns entities matching the search query, best match first within each scope.
    /// The way the query is interpreted depends on its value: a known hash searches
    /// the corresponding entities by hash, a number searches blocks by level, and any other
    /// string searches addresses by alias and tokens by name/symbol.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SearchResult>>> Get(SearchFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _search.Search(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
