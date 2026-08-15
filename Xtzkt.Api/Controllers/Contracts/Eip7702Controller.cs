using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Contracts;

[ApiController]
[Tags("Contracts")]
[Route("v1/eip7702")]
[Produces("application/json")]
public class Eip7702Controller(Eip7702DelegationRepository _delegations, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get EIP-7702 delegations
    /// </summary>
    /// <remarks>
    /// Returns EIP-7702 delegations — the authorizations that let a plain EVM address run a contract's
    /// code, which is what turns a regular wallet into a smart account. Each item records who authorized
    /// it, what it points to now, and what it pointed to before.
    ///
    /// Filter by `authority` to follow one address's delegation history. A `delegate` of `null`
    /// means the delegation was revoked and the address went back to being a plain one.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Eip7702Delegation>>> Get(Eip7702DelegationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _delegations.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _delegations.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get EIP-7702 delegations count
    /// </summary>
    /// <remarks>
    /// Returns the number of EIP-7702 delegations matching the filters — the same ones accepted
    /// by `/v1/eip7702`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(Eip7702DelegationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _delegations.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
