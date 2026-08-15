using System.Numerics;
using System.Text.Json;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;

namespace Xtzkt.Api.Repositories;

public class AssetRepository(AssetCache _assetCache, TokenRepository _tokenRepo)
{
    static readonly JsonDocumentOptions JsonOptions = new() { MaxDepth = 100_000 };
    static readonly string[] LogoKeys = ["thumbnailUri", "image", "logoURI", "displayUri"];

    public async Task<Asset?> Get(long tokenId)
    {
        var token = (await _tokenRepo.Get(new() { Id = new() { Eq = tokenId } }, new() { Limit = 1 }))
            .FirstOrDefault();

        if (token == null)
            return null;

        if (_assetCache.Get(token.Id) is Data.Models.Asset asset)
            return await FromAsset(asset, token);

        return FromToken(token);
    }

    public async Task<Asset?> Get(string contract, BigInteger tokenId, ChainInfoParameter? chain)
    {
        var tokens = (await _tokenRepo.Get(new TokenFilter
        {
            Chain = chain,
            Contract = new() { Hash = new() { Eq = contract } },
            TokenId = new() { Eq = tokenId },
        }, new Pagination { Limit = 2 })).ToList();

        if (tokens.Count == 0)
            return null;

        if (tokens.Count > 1)
            throw new BadRequestException("chain",
                "Tokens on several chains match the specified contract and token id. Specify the chain to pick one.");

        if (_assetCache.Get(tokens[0].Id) is Data.Models.Asset asset)
            return await FromAsset(asset, tokens[0]);

        return FromToken(tokens[0]);
    }

    async Task<Asset> FromAsset(Data.Models.Asset asset, Token token)
    {
        return new Asset
        {
            Name = asset.Name,
            Description = asset.Description,
            Logo = asset.Logo,
            Tokens = asset.Tokens.Length == 1 ? [token]
                : [.. await _tokenRepo.Get(new TokenFilter { Id = new() { In = [.. asset.Tokens] } }, new Pagination { Limit = 0 })]
        };
    }

    static Asset FromToken(Token token)
    {
        string? description = null;
        string? logo = null;

        if (token.Metadata is RawJson metadata)
        {
            using var doc = JsonDocument.Parse(metadata.ToString(), JsonOptions);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String)
                    description = d.GetString();

                foreach (var key in LogoKeys)
                {
                    if (doc.RootElement.TryGetProperty(key, out var l) && l.ValueKind == JsonValueKind.String && IsValidUri(l.GetString()))
                    {
                        logo = l.GetString();
                        break;
                    }
                }
            }
        }

        return new Asset
        {
            Name = token.Name,
            Description = description,
            Logo = logo,
            Tokens = [token],
        };
    }

    static bool IsValidUri(string? value)
    {
        return value != null && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "https" || uri.Scheme == "http" || uri.Scheme == "ipfs" || uri.Scheme == "data");
    }
}
