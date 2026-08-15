using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/register_constant")]
[Produces("application/json")]
public class RegisterConstantController(RegisterConstantRepository _registerConstants, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get register constant operations
    /// </summary>
    /// <remarks>
    /// Returns register global constant operations — Micheline expressions published on-chain
    /// so that contracts can reference them instead of inlining the code, which keeps them smaller
    /// and cheaper to deploy.
    ///
    /// Each one gets an `expr...` address to reference it by, and `refs` tells you how many contracts
    /// currently use it.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegisterConstantOperation>>> Get(RegisterConstantOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _registerConstants.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _registerConstants.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get register constant operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of register global constant operations matching the filters — the same ones
    /// accepted by `/v1/operations/register_constant`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(RegisterConstantOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _registerConstants.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
