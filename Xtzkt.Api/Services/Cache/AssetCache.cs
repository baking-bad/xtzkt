using Dapper;
using Npgsql;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class AssetCache
{
    readonly Lock Crit = new();
    // kept for the upcoming sync via db notifications
    readonly Dictionary<int, Asset> CachedById;
    readonly Dictionary<long, Asset> CachedByTokenId;

    public AssetCache(NpgsqlDataSource dataSource, ILogger<AssetCache> logger)
    {
        logger.LogDebug("Initializing asset cache...");

        using var db = dataSource.OpenConnection();
        var assets = db.Query<Asset>("""
            SELECT "Id", "Name", "Description", "Logo", "Tokens"
            FROM "Assets"
            """).ToList();

        CachedById = new(assets.Count);
        CachedByTokenId = new(assets.Sum(x => x.Tokens.Length));

        foreach (var asset in assets)
        {
            CachedById.Add(asset.Id, asset);
            foreach (var tokenId in asset.Tokens)
            {
                if (!CachedByTokenId.TryAdd(tokenId, asset))
                    logger.LogError("Token #{tokenId} belongs to both asset: #{first} and #{second}",
                        tokenId, CachedByTokenId[tokenId].Id, asset.Id);
            }
        }

        logger.LogInformation("Asset cache initialized with {cnt} items", CachedById.Count);
    }

    public Asset? Get(long tokenId)
    {
        lock (Crit)
        {
            return CachedByTokenId.GetValueOrDefault(tokenId);
        }
    }
}
