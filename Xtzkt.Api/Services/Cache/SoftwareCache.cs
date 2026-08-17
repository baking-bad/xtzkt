using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class SoftwareCache
{
    readonly IDbContextFactory<XtzktContext> DbFactory;
    readonly ILogger Logger;

    readonly Lock Crit = new();
    readonly Dictionary<int, Software> CachedById = [];
    readonly Dictionary<string, Software>[] CachedByShortHash = [[], [], [], [], [], [], [], []];
    readonly int[] LastLevels = new int[8];

    public SoftwareCache(IDbContextFactory<XtzktContext> dbFactory, ILogger<SoftwareCache> logger)
    {
        DbFactory = dbFactory;
        Logger = logger;

        Logger.LogDebug("Initializing software cache...");

        using var db = DbFactory.CreateDbContext();
        foreach (var software in db.Software.ToList())
        {
            CachedById.Add(software.Id, software);
            CachedByShortHash[software.ChainId].Add(software.ShortHash, software);
            LastLevels[software.ChainId] = Math.Max(LastLevels[software.ChainId], software.LastLevel);
        }

        Logger.LogInformation("Software cache initialized with {cnt} items", CachedById.Count);
    }

    public async Task OnStateChanged(int chainId, int minLevel, int lastLevel)
    {
        var cacheLevel = LastLevels[chainId];
        var lastValidLevel = Math.Min(cacheLevel, minLevel - 1);

        if (minLevel <= cacheLevel)
        {
            List<Software> reorged;
            lock (Crit)
            {
                reorged = [.. CachedById.Values.Where(x => x.ChainId == chainId && x.FirstLevel >= minLevel)];
                foreach (var software in reorged)
                {
                    CachedById.Remove(software.Id);
                    CachedByShortHash[software.ChainId].Remove(software.ShortHash);
                }
            }
            Logger.LogDebug("Removed {cnt} reorged software for chain #{chainId}", reorged.Count, chainId);
        }

        using var db = DbFactory.CreateDbContext();
        var updated = await db.Software
            .Where(x => x.ChainId == chainId && x.LastLevel > lastValidLevel)
            .ToListAsync();

        lock (Crit)
        {
            foreach (var software in updated)
            {
                CachedById[software.Id] = software;
                CachedByShortHash[software.ChainId][software.ShortHash] = software;
            }
        }

        Logger.LogDebug("Updated {cnt} software for chain #{chainId}", updated.Count, chainId);
        LastLevels[chainId] = lastLevel;
    }

    public Models.SoftwareInfo? GetInfo(int? id)
    {
        if (id is not int _id || Get(_id) is not Software software)
            return null;

        return new()
        {
            Id = software.Id,
            ShortHash = software.ShortHash,
        };
    }

    public async Task<Models.SoftwareInfo?> GetInfoAsync(int? id)
    {
        if (id is not int _id || await GetAsync(_id) is not Software software)
            return null;

        return new()
        {
            Id = software.Id,
            ShortHash = software.ShortHash,
        };
    }

    public Software? Get(int id)
    {
        if (!TryGetSafe(id, out var software))
        {
            using var db = DbFactory.CreateDbContext();
            software = db.Software.FirstOrDefault(x => x.Id == id);
            if (software != null) Add(software);
        }
        return software;
    }

    public async Task<Software?> GetAsync(int id)
    {
        if (!TryGetSafe(id, out var software))
        {
            using var db = DbFactory.CreateDbContext();
            software = await db.Software.FirstOrDefaultAsync(x => x.Id == id);
            if (software != null) Add(software);
        }
        return software;
    }

    bool TryGetSafe(int id, [NotNullWhen(true)] out Software? software)
    {
        lock (Crit)
        {
            return CachedById.TryGetValue(id, out software);
        }
    }

    void Add(Software software)
    {
        lock (Crit)
        {
            CachedById[software.Id] = software;
            CachedByShortHash[software.ChainId][software.ShortHash] = software;
        }
        Logger.LogDebug("Software {shortHash} cached", software.ShortHash);
    }
}
