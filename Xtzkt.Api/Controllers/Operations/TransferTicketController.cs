using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Operations;

[ApiController]
[Tags("Operations")]
[Route("v1/operations/transfer_ticket")]
[Produces("application/json")]
public class TransferTicketController(TransferTicketRepository _transferTickets, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get transfer ticket operations
    /// </summary>
    /// <remarks>
    /// Returns transfer ticket operations — the way a user (rather than a contract) sends tickets,
    /// since tickets can't be moved by a plain transaction. Each one carries the ticketer,
    /// the ticket content and the amount sent.
    ///
    /// The `layer` field says whether it happened on Tezos L1 or Tezos X.
    ///
    /// For the resulting balance changes rather than the operations themselves,
    /// use `/v1/tickets/transfers`.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransferTicketOperation>>> Get(TransferTicketOperationFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _transferTickets.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _transferTickets.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get transfer ticket operations count
    /// </summary>
    /// <remarks>
    /// Returns the number of transfer ticket operations matching the filters — the same ones
    /// accepted by `/v1/operations/transfer_ticket`. Handy for pagination controls.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(TransferTicketOperationFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _transferTickets.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
