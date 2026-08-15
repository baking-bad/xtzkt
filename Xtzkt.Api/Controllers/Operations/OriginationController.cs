using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/origination")]
[Produces("application/json")]
public class OriginationController(OriginationRepository _originations, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get origination operations
    /// </summary>
    /// <remarks>
    /// Returns originations — contract deployments. Each one carries the deployed contract,
    /// its code hash and the balance it was funded with.
    ///
    /// The `env` field says where the contract was deployed (`l1`, `x_evm` or `x_michelson`),
    /// and therefore which fields to expect.
    ///
    /// Filter by `contractCodeHash` to find every deployment of the same code.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OriginationOperation>>> Get(OriginationOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _originations.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _originations.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get origination operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of originations matching the filters — the same ones accepted
    /// by `/v1/operations/origination`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(OriginationOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _originations.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
