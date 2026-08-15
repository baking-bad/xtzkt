using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Assets;

[ApiController]
[Tags("Assets")]
[Route("v1/tickets")]
[Produces("application/json")]
public class TicketsController(
    TicketRepository _tickets,
    TicketBalanceRepository _balances,
    TicketTransferRepository _transfers,
    ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get tickets
    /// </summary>
    /// <remarks>
    /// Returns tickets — protocol-level assets minted by a contract (the ticketer) with arbitrary
    /// Michelson content attached. A ticket is identified by its ticketer plus its content,
    /// so one contract can issue many different tickets.
    ///
    /// `typeHash` and `contentHash` are the cheap way to find tickets of the same shape:
    /// filter by them instead of comparing raw Micheline client-side.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Ticket>>> Get(TicketFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _tickets.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _tickets.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get tickets count
    /// </summary>
    /// <remarks>
    /// Returns the number of tickets matching the filters — the same ones accepted by `/v1/tickets`.
    /// Handy for pagination controls, when you need the total without fetching the items themselves.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(TicketFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _tickets.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get ticket balances
    /// </summary>
    /// <remarks>
    /// Returns how much of a ticket each address holds, along with the ticket itself, so a holdings
    /// page can be rendered from one request. Filter by `address` for someone's tickets, or by `ticket`
    /// for a ticket's holders.
    ///
    /// Zero balances are kept, so add `balance.gt=0` if you only want current holders.
    /// </remarks>
    [HttpGet("balances")]
    public async Task<ActionResult<IEnumerable<TicketBalance>>> GetBalances(TicketBalanceFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _balances.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _balances.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get ticket balances count
    /// </summary>
    /// <remarks>
    /// Returns the number of ticket balances matching the filters — the same ones accepted by
    /// `/v1/tickets/balances`. With `ticket` and `balance.gt=0` it gives you a ticket's holders count.
    /// </remarks>
    [HttpGet("balances/count")]
    public async Task<ActionResult<long>> GetBalancesCount(TicketBalanceFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _balances.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get ticket transfers
    /// </summary>
    /// <remarks>
    /// Returns individual ticket transfers with sender, target, amount and the operation that caused them.
    /// Mints have no `from`, burns have no `to`.
    ///
    /// To get everything related to an address in one feed, use `anyof.from.to=...` instead of two requests.
    /// </remarks>
    [HttpGet("transfers")]
    public async Task<ActionResult<IEnumerable<TicketTransfer>>> GetTransfers(TicketTransferFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _transfers.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _transfers.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get ticket transfers count
    /// </summary>
    /// <remarks>
    /// Returns the number of ticket transfers matching the filters — the same ones accepted by
    /// `/v1/tickets/transfers`. Handy for pagination controls, or for a ticket's activity counter.
    /// </remarks>
    [HttpGet("transfers/count")]
    public async Task<ActionResult<long>> GetTransfersCount(TicketTransferFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _transfers.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
