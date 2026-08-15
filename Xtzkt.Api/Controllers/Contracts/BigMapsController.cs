using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Contracts;

[ApiController]
[Tags("Contracts")]
[Route("v1/bigmaps")]
[Produces("application/json")]
public class BigMapsController(
    BigMapRepository _bigMaps,
    BigMapKeyRepository _bigMapKeys,
    BigMapUpdateRepository _bigMapUpdates,
    ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get bigmaps
    /// </summary>
    /// <remarks>
    /// Returns bigmaps — lazily-loaded maps stored outside a contract's main storage, typically holding
    /// ledgers, allowances or token metadata. Each one comes with its key and value types and the path
    /// it sits at in the contract storage.
    ///
    /// Bigmaps the indexer recognized are marked with `tags` (such as `ledger`), which is the quickest
    /// way to find a token contract's balance map.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BigMap>>> Get(BigMapFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _bigMaps.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _bigMaps.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get bigmaps count
    /// </summary>
    /// <remarks>
    /// Returns the number of bigmaps matching the filters — the same ones accepted by `/v1/bigmaps`.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(BigMapFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _bigMaps.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get bigmap keys
    /// </summary>
    /// <remarks>
    /// Returns the current content of bigmaps — one item per key, with its latest value in both
    /// JSON and raw Micheline form.
    ///
    /// Removed keys are kept with `active=false` and their last value, so add `active=true`
    /// to get only what's actually in the bigmap right now.
    ///
    /// Keys can be matched by content (`key.someField=...`) as well as by `key` or `keyHash`,
    /// which is what you want for looking up a single holder in a ledger.
    /// </remarks>
    [HttpGet("keys")]
    public async Task<ActionResult<IEnumerable<BigMapKey>>> GetKeys(BigMapKeyFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _bigMapKeys.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _bigMapKeys.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get bigmap keys count
    /// </summary>
    /// <remarks>
    /// Returns the number of bigmap keys matching the filters — the same ones accepted by
    /// `/v1/bigmaps/keys`. With `bigMap` and `active=true` it gives you a bigmap's current size.
    /// </remarks>
    [HttpGet("keys/count")]
    public async Task<ActionResult<long>> GetKeysCount(BigMapKeyFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _bigMapKeys.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get bigmap updates
    /// </summary>
    /// <remarks>
    /// Returns the history of bigmap changes — every key added, updated or removed, with the value
    /// it was set to and the operation that did it. The `action` field says which kind of change it was.
    ///
    /// This is the endpoint for "how did this value get here": filter by `bigMap` and `keyHash`
    /// to get the full history of a single key.
    /// </remarks>
    [HttpGet("updates")]
    public async Task<ActionResult<IEnumerable<BigMapUpdate>>> GetUpdates(BigMapUpdateFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _bigMapUpdates.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _bigMapUpdates.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get bigmap updates count
    /// </summary>
    /// <remarks>
    /// Returns the number of bigmap updates matching the filters — the same ones accepted by
    /// `/v1/bigmaps/updates`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("updates/count")]
    public async Task<ActionResult<long>> GetUpdatesCount(BigMapUpdateFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _bigMapUpdates.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
