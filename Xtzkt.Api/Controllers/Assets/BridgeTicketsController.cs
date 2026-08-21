using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Assets;

[ApiController]
[Tags("Assets")]
[Route("v1/bridge_tickets")]
[Produces("application/json")]
public class BridgeTicketsController(
    BridgeTicketRepository _tickets,
    BridgeTicketBalanceRepository _balances,
    BridgeTicketTransferRepository _transfers,
    ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get bridge tickets
    /// </summary>
    /// <remarks>
    /// Returns bridge tickets — L1 tickets bridged to Tezos X via the FA bridge. On Tezos X such
    /// a ticket is tracked by `weakHash`, the hash of its L1 ticketer and content (the content type
    /// is not hashed, so it's a lookup key rather than an identity), and its balances back the
    /// deposited assets: the bridge credits them on deposits and debits them on withdrawals,
    /// there are no other movements.
    ///
    /// `totalSupply` of a bridge ticket is the amount currently bridged in, and `totalMinted`/`totalBurned`
    /// accumulate everything ever deposited/withdrawn.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BridgeTicket>>> Get(BridgeTicketFilter filter, Pagination pagination, Selection selection)
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
    /// Get bridge tickets count
    /// </summary>
    /// <remarks>
    /// Returns the number of bridge tickets matching the filters — the same ones accepted by
    /// `/v1/bridge_tickets`. Handy for pagination controls, when you need the total without
    /// fetching the items themselves.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(BridgeTicketFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _tickets.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get bridge ticket balances
    /// </summary>
    /// <remarks>
    /// Returns how much of a bridge ticket each address holds. The holder is either an ERC20 proxy
    /// contract, backing the wrapped token supply, or a regular address, when the deposit was made
    /// without a proxy (or the proxy call failed) — in that case this balance is the only place
    /// the deposited funds exist on the chain.
    ///
    /// Zero balances are kept, so add `balance.gt=0` if you only want current holders.
    /// </remarks>
    [HttpGet("balances")]
    public async Task<ActionResult<IEnumerable<BridgeTicketBalance>>> GetBalances(BridgeTicketBalanceFilter filter, Pagination pagination, Selection selection)
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
    /// Get bridge ticket balances count
    /// </summary>
    /// <remarks>
    /// Returns the number of bridge ticket balances matching the filters — the same ones accepted by
    /// `/v1/bridge_tickets/balances`. With `ticket` and `balance.gt=0` it gives you a bridge ticket's
    /// holders count.
    /// </remarks>
    [HttpGet("balances/count")]
    public async Task<ActionResult<long>> GetBalancesCount(BridgeTicketBalanceFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _balances.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get bridge ticket transfers
    /// </summary>
    /// <remarks>
    /// Returns individual bridge ticket transfers with the operation that caused them. There are only
    /// two kinds: credits, bridging funds in from L1 (no `from`), and debits, withdrawing them back
    /// to L1 (no `to`) — bridge tickets never move between addresses on the chain.
    ///
    /// Credits carry `depositId`, linking them back to the deposit operation that caused them.
    /// A deposit that was queued instead of being credited right away is credited later by a separate
    /// `claim` transaction, so its credit belongs to that transaction, not to the deposit — which
    /// deposit it claimed is on the deposit itself (`claimTransactionId`), not here. Mind that `xtz`
    /// deposits never produce a bridge ticket transfer at all — they credit the native balance instead.
    /// </remarks>
    [HttpGet("transfers")]
    public async Task<ActionResult<IEnumerable<BridgeTicketTransfer>>> GetTransfers(BridgeTicketTransferFilter filter, Pagination pagination, Selection selection)
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
    /// Get bridge ticket transfers count
    /// </summary>
    /// <remarks>
    /// Returns the number of bridge ticket transfers matching the filters — the same ones accepted by
    /// `/v1/bridge_tickets/transfers`. Handy for pagination controls, or for a bridge ticket's
    /// activity counter.
    /// </remarks>
    [HttpGet("transfers/count")]
    public async Task<ActionResult<long>> GetTransfersCount(BridgeTicketTransferFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _transfers.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
