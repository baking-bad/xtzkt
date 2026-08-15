using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/increase_paid_storage")]
[Produces("application/json")]
public class IncreasePaidStorageController(IncreasePaidStorageRepository _increasePaidStorage, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get increase paid storage operations
    /// </summary>
    /// <remarks>
    /// Returns increase paid storage operations — someone paying upfront for extra storage on
    /// a contract, so the contract can grow later without its own callers being charged for it.
    /// Anyone can do this for any contract.
    ///
    /// The `layer` field says whether it happened on Tezos L1 or Tezos X.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<IncreasePaidStorageOperation>>> Get(IncreasePaidStorageOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _increasePaidStorage.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _increasePaidStorage.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get increase paid storage operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of increase paid storage operations matching the filters — the same ones
    /// accepted by `/v1/operations/increase_paid_storage`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(IncreasePaidStorageOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _increasePaidStorage.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
