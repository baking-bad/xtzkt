using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Accounts;

[ApiController]
[Tags("Accounts")]
[Route("v1/domains")]
[Produces("application/json")]
public class DomainsController(DomainRepository _domains, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get domains
    /// </summary>
    /// <remarks>
    /// Returns Tezos Domains names with their owner, the address they resolve to and their expiration.
    /// Filter by `name` to resolve a name, or by `address` with `reverse=true` to get the primary name of an address.
    ///
    /// Expired domains are not removed, so add `expiration.gt` with the current time to get only the active ones.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Domain>>> Get(DomainFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _domains.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _domains.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get domains count
    /// </summary>
    /// <remarks>
    /// Returns the number of domains matching the filters — the same ones accepted by `/v1/domains`.
    /// Handy for pagination controls, when you need the total without fetching the items themselves.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(DomainFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _domains.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
