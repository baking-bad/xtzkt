using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/transaction")]
[Produces("application/json")]
public class TransactionController(TransactionRepository _transactions, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get transaction operations
    /// </summary>
    /// <remarks>
    /// Returns transactions — transfers of value and contract calls, the most common operation there is.
    /// Internal transactions made by contracts are returned alongside the top-level ones.
    ///
    /// The `direction` field says which runtimes the transaction went between (`l1`, `x_evm`, `x_michelson`,
    /// or one of the cross-runtime pairs), and therefore which fields to expect. Note that cross-runtime
    /// transactions carry both a sent and a received amount, in different decimals.
    ///
    /// Use `anyof.sender.target=...` to get everything related to an address in one request,
    /// and `select` to keep responses small — decoded parameters can be bulky.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionOperation>>> Get(TransactionOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _transactions.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _transactions.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get transaction operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of transactions matching the filters — the same ones accepted
    /// by `/v1/operations/transaction`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(TransactionOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _transactions.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
