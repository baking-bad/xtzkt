using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Chains;

[ApiController]
[Tags("Chains")]
[Route("v1/chains")]
[Produces("application/json")]
public class ChainsController(ChainRepository _chains, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get chains
    /// </summary>
    /// <remarks>
    /// Returns the chains this instance indexes, together with their sync state — the last indexed block
    /// and the last block known to the node. Comparing `level` with `knownLevel` tells you whether
    /// the indexer is caught up.
    ///
    /// Start here to discover which `chain` values the other endpoints accept, and to find the current
    /// head level before querying anything level-based.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Chain>>> Get(ChainFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _chains.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _chains.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get chains count
    /// </summary>
    /// <remarks>
    /// Returns the number of chains matching the filters — the same ones accepted by `/v1/chains`.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(ChainFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _chains.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
