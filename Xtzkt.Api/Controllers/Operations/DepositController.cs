using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/deposit")]
[Produces("application/json")]
public class DepositController(DepositRepository _deposits, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get deposit operations
    /// </summary>
    /// <remarks>
    /// Returns deposits — funds bridged from Tezos L1 into Tezos X, credited to an address there.
    /// The `type` field says what was deposited: `xtz` for native tez, `fa` for an FA token.
    ///
    /// The `runtime` field says which runtime the funds landed in, and therefore which fields to expect.
    ///
    /// A deposit with a `depositId` was queued rather than credited right away — the funds
    /// stay on the bridge until claimed.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepositOperation>>> Get(DepositOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _deposits.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _deposits.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get deposit operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of deposits matching the filters — the same ones accepted
    /// by `/v1/operations/deposit`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(DepositOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _deposits.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
