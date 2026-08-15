using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Chains;

[ApiController]
[Tags("Chains")]
[Route("v1/blocks")]
[Produces("application/json")]
public class BlocksController(BlockRepository _blocks, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get blocks
    /// </summary>
    /// <remarks>
    /// Returns blocks with their hash, timestamp, protocol and layer-specific details — baker, rewards
    /// and fees on Tezos L1, sequencer pool and fees on Tezos X. The `layer` field tells you which
    /// kind a block is, and therefore which extra fields it carries.
    ///
    /// The same `level` exists on every indexed chain, so add `chain` unless you want all of them at once.
    /// To see what happened inside a block, use `/v1/activity/block`.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Block>>> Get(BlockFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _blocks.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _blocks.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get blocks count
    /// </summary>
    /// <remarks>
    /// Returns the number of blocks matching the filters — the same ones accepted by `/v1/blocks`.
    /// Handy for pagination controls, when you need the total without fetching the items themselves.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(BlockFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _blocks.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
