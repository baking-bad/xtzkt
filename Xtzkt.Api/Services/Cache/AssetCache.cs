using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class AssetCache
{
    readonly IDbContextFactory<XtzktContext> DbFactory;
    readonly ILogger Logger;
    readonly Lock Crit = new();
    readonly SemaphoreSlim Sema = new(1, 1);

    Dictionary<int, Asset> CachedById;
    Dictionary<long, Asset> CachedByTokenId;

    public AssetCache(IDbContextFactory<XtzktContext> dbFactory, ILogger<AssetCache> logger)
    {
        DbFactory = dbFactory;
        Logger = logger;

        Logger.LogDebug("Initializing asset cache...");

        using var db = DbFactory.CreateDbContext();
        (CachedById, CachedByTokenId) = Build([.. db.Assets]);

        Logger.LogInformation("Asset cache initialized with {cnt} items", CachedById.Count);
    }

    public Asset? Get(long tokenId)
    {
        lock (Crit)
        {
            return CachedByTokenId.GetValueOrDefault(tokenId);
        }
    }

    /// <summary>
    /// Reloads all the assets.
    /// </summary>
    public async Task ReloadAsync()
    {
        await Sema.WaitAsync();
        try
        {
            using var db = DbFactory.CreateDbContext();
            var (byId, byTokenId) = Build(await db.Assets.ToListAsync());

            lock (Crit)
            {
                CachedById = byId;
                CachedByTokenId = byTokenId;
            }

            Logger.LogDebug("Asset cache reloaded with {cnt} items", byId.Count);
        }
        finally
        {
            Sema.Release();
        }
    }

    /// <summary>
    /// Reloads the changed assets, dropping the ones that no longer exist.
    /// </summary>
    public async Task UpdateAsync(int[] ids)
    {
        await Sema.WaitAsync();
        try
        {
            using var db = DbFactory.CreateDbContext();
            var assets = await db.Assets
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();

            lock (Crit)
            {
                foreach (var id in ids)
                    Remove(id);

                foreach (var asset in assets)
                    Add(asset);
            }

            Logger.LogDebug("Asset cache updated with {cnt} changed items", ids.Length);
        }
        finally
        {
            Sema.Release();
        }
    }

    void Add(Asset asset)
    {
        CachedById[asset.Id] = asset;
        foreach (var tokenId in asset.Tokens)
            CachedByTokenId[tokenId] = asset;
    }

    void Remove(int id)
    {
        if (!CachedById.Remove(id, out var asset))
            return;

        foreach (var tokenId in asset.Tokens)
        {
            // the token may have already moved to another asset, reloaded earlier
            if (CachedByTokenId.TryGetValue(tokenId, out var owner) && owner.Id == id)
                CachedByTokenId.Remove(tokenId);
        }
    }

    (Dictionary<int, Asset>, Dictionary<long, Asset>) Build(List<Asset> assets)
    {
        var byId = new Dictionary<int, Asset>(assets.Count);
        var byTokenId = new Dictionary<long, Asset>(assets.Sum(x => x.Tokens.Length));

        foreach (var asset in assets)
        {
            byId.Add(asset.Id, asset);
            foreach (var tokenId in asset.Tokens)
            {
                if (!byTokenId.TryAdd(tokenId, asset))
                    Logger.LogError("Token #{tokenId} belongs to both asset: #{first} and #{second}",
                        tokenId, byTokenId[tokenId].Id, asset.Id);
            }
        }

        return (byId, byTokenId);
    }
}
