using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class ProtocolCache
{
    readonly IDbContextFactory<XtzktContext> DbFactory;
    readonly ChainCache ChainCache;
    readonly ILogger Logger;

    readonly List<Protocol>[] Cache = [[], [], [], [], [], [], [], []];
    readonly Dictionary<int, Protocol> CachedById = [];

    public ProtocolCache(IDbContextFactory<XtzktContext> dbFactory, ChainCache _chainCache, ILogger<ProtocolCache> logger)
    {
        DbFactory = dbFactory;
        ChainCache = _chainCache;
        Logger = logger;

        Logger.LogDebug("Initializing protocol cache...");

        using var db = DbFactory.CreateDbContext();
        var protocols = db.Protocols.OrderBy(x => x.Id).ToList();

        foreach (var protocol in protocols)
        {
            Cache[protocol.ChainId].Add(protocol);
            CachedById.Add(protocol.Id, protocol);
        }
        
        Logger.LogInformation("Protocol cache initialized with {cnt} items", Cache.Sum(x => x.Count));
    }

    public async Task OnStateChanged(int chainId, int minLevel, int lastLevel)
    {
        var protocols = Cache[chainId];

        if (protocols.Count == 0 ||
            protocols.Count != ChainCache.Get(chainId).ProtocolsCount ||
            minLevel < protocols[^1].FirstLevel)
        {
            using var db = DbFactory.CreateDbContext();
            Cache[chainId] = await db.Protocols.Where(x => x.ChainId == chainId).OrderBy(x => x.Id).ToListAsync();
            foreach (var protocol in Cache[chainId])
                CachedById[protocol.Id] = protocol;

            Logger.LogInformation("Updated {cnt} protocols for chain #{chainId}", Cache[chainId].Count, chainId);
        }
    }

    public Models.ProtocolInfo GetInfo(int id)
    {
        var protocol = Get(id);
        return new()
        {
            Id = protocol.Id,
            Hash = protocol.Hash,
            Version = protocol.Version,
        };
    }

    public Protocol Get(int id)
    {
        if (!CachedById.TryGetValue(id, out var protocol))
        {
            // should never get here, but still...
            Logger.LogWarning("Inconsistent cache");
            using var db = DbFactory.CreateDbContext();
            protocol = db.Protocols.First(x => x.Id == id);
        }
        return protocol;
    }

    public Protocol GetCurrent(int chainId)
    {
        if (Cache[chainId].Count == 0)
        {
            // should never get here, but still...
            Logger.LogWarning("Inconsistent cache");
            using var db = DbFactory.CreateDbContext();
            Cache[chainId] = [.. db.Protocols.Where(x => x.ChainId == chainId).OrderBy(x => x.Id)];
        }
        return Cache[chainId][^1];
    }
}
