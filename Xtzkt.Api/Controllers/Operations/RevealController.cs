using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/reveal")]
[Produces("application/json")]
public class RevealController(RevealRepository _reveals, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get reveal operations
    /// </summary>
    /// <remarks>
    /// Returns reveals — the operation that publishes an address's public key on-chain.
    /// Every address must reveal once before it can send anything else, so this is usually
    /// the first operation in an account's history.
    ///
    /// The `layer` field says whether it happened on Tezos L1 or Tezos X.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RevealOperation>>> Get(ManagerOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _reveals.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _reveals.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get reveal operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of reveals matching the filters — the same ones accepted
    /// by `/v1/operations/reveal`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(ManagerOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _reveals.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
