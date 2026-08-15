using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/migration")]
[Produces("application/json")]
public class MigrationController(MigrationRepository _migrations, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get migration operations
    /// </summary>
    /// <remarks>
    /// Returns migrations — balance and contract changes made by the protocol itself, not by anyone's
    /// operation. They happen on protocol upgrades, airdrops, and similar events, so they have no hash,
    /// no sender and no fees. The `kind` field says what caused it.
    ///
    /// They matter because they move funds and rewrite contracts: ignore them and a reconstructed
    /// balance history won't add up.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MigrationOperation>>> Get(MigrationOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _migrations.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _migrations.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get migration operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of migrations matching the filters — the same ones accepted
    /// by `/v1/operations/migration`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(MigrationOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _migrations.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
