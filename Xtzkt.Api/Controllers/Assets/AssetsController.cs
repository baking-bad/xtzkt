using System.Numerics;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Repositories;
using Xtzkt.Api.Services.ResponseCache;
using Xtzkt.Api.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Api.Controllers.Assets;

[ApiController]
[Tags("Assets")]
[Route("v1/assets")]
[Produces("application/json")]
public class AssetsController(AssetRepository _assets, ResponseCacheService _responseCache) : ControllerBase
{
    /// <summary>
    /// Get asset by token id
    /// </summary>
    /// <remarks>
    /// Returns the asset the specified token belongs to: its name, description, logo and all the tokens
    /// it consists of. Any of the asset's own tokens can be used for the lookup, they all resolve to the
    /// same asset. A token that doesn't belong to any asset is returned as an asset of its own, described
    /// by its own metadata. Returns `null` if there's no such token.
    /// </remarks>
    /// <param name="tokenId">Internal unique token id.</param>
    [HttpGet("{tokenId:long}")]
    public async Task<ActionResult<Asset?>> GetByTokenId(long tokenId)
    {
        // we use normalized values instead of Request.Path for better cache keys matching
        var query = ResponseCacheService.BuildKey($"/v1/assets/{tokenId}");

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _assets.Get(tokenId);

        return this.Bytes(_responseCache.Set(query, res));
    }

    /// <summary>
    /// Get asset by contract
    /// </summary>
    /// <remarks>
    /// Returns the asset the specified token belongs to: its name, description, logo and all the tokens
    /// it consists of. Any of the asset's own tokens can be used for the lookup, they all resolve to the
    /// same asset. A token that doesn't belong to any asset is returned as an asset of its own, described
    /// by its own metadata. Returns `null` if there's no such token.
    /// </remarks>
    /// <param name="contract">Address of the token contract.</param>
    /// <param name="tokenId">Token id within the contract.</param>
    /// <param name="chain">
    /// Chain to look the token up on. Only needed when tokens on several chains match the specified
    /// contract and token id, which is reported with an error.
    /// </param>
    [HttpGet("{contract}/{tokenId}")]
    public async Task<ActionResult<Asset?>> GetByContract(string contract, string tokenId, ChainInfoParameter? chain)
    {
        if (!AddressHash.TryNormalize(contract, out var normalizedContract))
            throw new BadRequestException(nameof(contract), "Invalid contract address");

        if (!Regexes.Number().IsMatch(tokenId) || !BigInteger.TryParse(tokenId, out var normalizedTokenId))
            throw new BadRequestException(nameof(tokenId), "Invalid token id");

        // we use normalized values instead of Request.Path for better cache keys matching
        var query = ResponseCacheService.BuildKey($"/v1/assets/{normalizedContract}/{normalizedTokenId}",
            ("chain", chain));

        if (_responseCache.TryGet(query, out var cached))
            return this.Bytes(cached);

        var res = await _assets.Get(normalizedContract, normalizedTokenId, chain);

        return this.Bytes(_responseCache.Set(query, res));
    }
}
