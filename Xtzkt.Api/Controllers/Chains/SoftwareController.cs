using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Chains;

[ApiController]
[Tags("Chains")]
[Route("v1/software")]
[Produces("application/json")]
public class SoftwareController(SoftwareRepository _software, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get software
    /// </summary>
    /// <remarks>
    /// Returns baker software builds detected from the blocks they produced, with the level range
    /// and the number of blocks each build made. Useful for tracking how quickly bakers adopt new releases.
    ///
    /// Builds are identified by a short commit hash taken from the block header, so a build is only
    /// listed once it has produced at least one block, and it can't always be mapped to a release version.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Software>>> Get(SoftwareFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _software.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _software.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get software count
    /// </summary>
    /// <remarks>
    /// Returns the number of software builds matching the filters — the same ones accepted by `/v1/software`.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(SoftwareFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _software.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
