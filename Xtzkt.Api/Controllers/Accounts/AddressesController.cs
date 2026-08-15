using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Accounts;

[ApiController]
[Tags("Accounts")]
[Route("v1/addresses")]
[Produces("application/json")]
public class AddressesController(AddressRepository _addresses, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get addresses
    /// </summary>
    /// <remarks>
    /// Returns a list of addresses of all kinds — users, bakers, contracts, smart rollups — with their
    /// balances, counters and type-specific details. The `type` field tells you which kind an address is,
    /// and therefore which extra fields it carries.
    ///
    /// An address is a single hash on a single chain, so the same hash may appear more than once.
    /// Add `chain` when you mean a particular one. To get all addresses of the same party at once
    /// (including Tezos X aliases in other runtimes), use `/v1/accounts/{address}` instead.
    ///
    /// Use `select` to fetch just the fields you need — it makes responses noticeably smaller and faster.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Address>>> Get(AddressFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _addresses.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _addresses.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get addresses count
    /// </summary>
    /// <remarks>
    /// Returns the number of addresses matching the filters — the same ones accepted by `/v1/addresses`.
    /// Handy for pagination controls, when you need the total without fetching the items themselves.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(AddressFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _addresses.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
