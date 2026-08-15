using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Services.ResponseCache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Controllers.Accounts;

[ApiController]
[Tags("Accounts")]
[Route("v1/accounts")]
[Produces("application/json")]
public class AccountsController(AccountRepository _accounts, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get account
    /// </summary>
    /// <remarks>
    /// Returns the account the specified address belongs to: its canonical hash and all the addresses
    /// it owns — the same hash on other chains and layers, plus Tezos X aliases in other runtimes.
    /// Any of the account's own address hashes can be used for the lookup, they all resolve to the same
    /// account. Returns `null` if there's no such address.
    /// </remarks>
    /// <param name="address">Any address of the account (`tz`, `KT`, `sr`, `0x`).</param>
    [HttpGet("{address}")]
    public async Task<ActionResult<Account?>> Get(string address)
    {
        if (!AddressHash.TryNormalize(address, out var normalizedAddress))
            throw new BadRequestException(nameof(address), "Invalid address hash");

        // we use normalized hash instead of Request.Path for better cache keys matching
        var query = ResponseCacheService.BuildKey($"/v1/accounts/{normalizedAddress}");

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _accounts.Get(normalizedAddress);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
