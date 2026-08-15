using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Contracts;

[ApiController]
[Tags("Contracts")]
[Route("v1/storages")]
[Produces("application/json")]
public class StoragesController(StorageRepository _storages, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get storages
    /// </summary>
    /// <remarks>
    /// Returns contract storage snapshots — one per change, in both JSON and raw Micheline form,
    /// along with the operation that caused it. Filter by `contract` to get a contract's storage history.
    ///
    /// Add `current=true` to get just the latest state of a contract's storage.
    ///
    /// Bigmaps appear here as their integer pointers, not their content — use `/v1/bigmaps/keys` for that.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Storage>>> Get(StorageFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _storages.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _storages.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get storages count
    /// </summary>
    /// <remarks>
    /// Returns the number of storage snapshots matching the filters — the same ones accepted
    /// by `/v1/storages`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(StorageFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _storages.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
