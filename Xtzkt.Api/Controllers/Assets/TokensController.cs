using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Responses;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Controllers.Assets;

[ApiController]
[Tags("Assets")]
[Route("v1/tokens")]
[Produces("application/json")]
public class TokensController(
    TokenRepository _tokens,
    TokenBalanceRepository _balances,
    TokenTransferRepository _transfers,
    ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get tokens
    /// </summary>
    /// <remarks>
    /// Returns tokens (FA1, FA1.2, FA2, ERC20, ERC721, ERC1155) with their standard, metadata, total supply
    /// and holders count. A token is identified by its contract plus a token id within that contract.
    ///
    /// Tokens of the same real-world asset deployed on different chains are separate tokens here,
    /// each with its own supply and decimals. Use `/v1/assets` to see them as one thing.
    ///
    /// Use `select` to fetch just the fields you need — metadata can be bulky.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Token>>> Get(TokenFilter filter, Pagination pagination, Selection selection)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter), ("pagination", pagination), ("selection", selection));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        object res;
        if (selection.Select == null)
        {
            res = await _tokens.Get(filter, pagination);
        }
        else
        {
            res = new SelectionResponse
            {
                Cols = selection.Cols(),
                Rows = await _tokens.Get(filter, pagination, selection)
            };
        }

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get tokens count
    /// </summary>
    /// <remarks>
    /// Returns the number of tokens matching the filters — the same ones accepted by `/v1/tokens`.
    /// Handy for pagination controls, when you need the total without fetching the items themselves.
    /// </remarks>
    [HttpGet("count")]
    public async Task<ActionResult<long>> GetCount(TokenFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _tokens.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get token balances
    /// </summary>
    /// <remarks>
    /// Returns how much of a token each address holds, along with the token itself, so a portfolio
    /// can be rendered from one request. Filter by `address` for someone's holdings, or by `token`
    /// for a token's holders.
    ///
    /// Balances are raw on-chain amounts — divide by `10^decimals` from the token metadata to display them.
    /// Zero balances are kept, so add `balance.gt=0` if you only want current holders.
    /// </remarks>
    [HttpGet("balances")]
    public async Task<ActionResult<IEnumerable<TokenBalance>>> GetBalances(TokenBalanceFilter filter, Pagination pagination, Selection selection)
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
    /// Get token balances count
    /// </summary>
    /// <remarks>
    /// Returns the number of token balances matching the filters — the same ones accepted by
    /// `/v1/tokens/balances`. With `token` and `balance.gt=0` it gives you a token's holders count.
    /// </remarks>
    [HttpGet("balances/count")]
    public async Task<ActionResult<long>> GetBalancesCount(TokenBalanceFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _balances.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get token transfers
    /// </summary>
    /// <remarks>
    /// Returns individual token transfers with sender, target, amount and the operation that caused them.
    /// Mints have no `from`, burns have no `to`.
    ///
    /// To get everything related to an address in one feed, use `anyof.from.to=...` instead of two requests.
    ///
    /// Amounts are raw on-chain values — divide by `10^decimals` from the token metadata to display them.
    /// </remarks>
    [HttpGet("transfers")]
    public async Task<ActionResult<IEnumerable<TokenTransfer>>> GetTransfers(TokenTransferFilter filter, Pagination pagination, Selection selection)
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
    /// Get token transfers count
    /// </summary>
    /// <remarks>
    /// Returns the number of token transfers matching the filters — the same ones accepted by
    /// `/v1/tokens/transfers`. Handy for pagination controls, or for a token's activity counter.
    /// </remarks>
    [HttpGet("transfers/count")]
    public async Task<ActionResult<long>> GetTransfersCount(TokenTransferFilter filter)
    {
        var query = ResponseCacheService.BuildKey(Request.Path.Value,
            ("filter", filter));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _transfers.Count(filter);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
