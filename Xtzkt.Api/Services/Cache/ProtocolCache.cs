using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class ProtocolCache
{
    readonly IDbContextFactory<XtzktContext> DbFactory;
    readonly ChainCache ChainCache;
    readonly ILogger Logger;

    readonly Lock Crit = new();
    readonly List<Protocol>[] Cache = [[], [], [], [], [], [], [], []];
    readonly Dictionary<int, Protocol> CachedById = [];

    public ProtocolCache(IDbContextFactory<XtzktContext> dbFactory, ChainCache _chainCache, ILogger<ProtocolCache> logger)
    {
        DbFactory = dbFactory;
        ChainCache = _chainCache;
        Logger = logger;
        ResetCache();
    }

    public async Task OnStateChanged(int chainId, int minLevel, int lastLevel)
    {
        var protocolsCount = ChainCache.Get(chainId).ProtocolsCount; // TODO: wait for ChainCache

        bool reset;
        lock (Crit)
        {
            var protocols = Cache[chainId];
            reset = protocols.Count == 0 ||
                protocols.Count != protocolsCount ||
                minLevel < protocols[^1].FirstLevel;
        }

        if (reset) ResetCache(chainId);
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
        if (!TryGetSafe(id, out var protocol))
        {
            // should never get here, but still...
            Logger.LogWarning("Inconsistent cache");
            ResetCache();

            if (!TryGetSafe(id, out protocol))
                throw new Exception($"Protocol #{id} doesn't exist");
        }
        return protocol;
    }

    public Protocol GetCurrent(int chainId)
    {
        if (!TryGetCurrentSafe(chainId, out var protocol))
        {
            // should never get here, but still...
            Logger.LogWarning("Inconsistent cache");
            ResetCache(chainId);

            if (!TryGetCurrentSafe(chainId, out protocol))
                throw new Exception($"Protocols for chain #{chainId} don't exist");
        }
        return protocol;
    }

    void ResetCache()
    {
        Logger.LogDebug("Initializing protocol cache...");

        using var db = DbFactory.CreateDbContext();
        var protocols = db.Protocols.OrderBy(x => x.Id).ToList();

        lock (Crit)
        {
            for (int i = 0; i < Cache.Length; i++) Cache[i].Clear();
            CachedById.Clear();
            
            foreach (var protocol in protocols)
            {
                Cache[protocol.ChainId].Add(protocol);
                CachedById.Add(protocol.Id, protocol);
            }
        }

        Logger.LogInformation("Protocol cache initialized with {cnt} items", protocols.Count);
    }

    void ResetCache(int chainId)
    {
        using var db = DbFactory.CreateDbContext();
        var protocols = db.Protocols.Where(x => x.ChainId == chainId).OrderBy(x => x.Id).ToList();

        lock (Crit)
        {
            Cache[chainId] = protocols;
            foreach (var protocol in protocols)
                CachedById[protocol.Id] = protocol;
        }

        Logger.LogInformation("Updated {cnt} protocols for chain #{chainId}", protocols.Count, chainId);

    }

    bool TryGetSafe(int id, [NotNullWhen(true)] out Protocol? protocol)
    {
        lock (Crit)
        {
            return CachedById.TryGetValue(id, out protocol);
        }
    }

    bool TryGetCurrentSafe(int chainId, [NotNullWhen(true)] out Protocol? protocol)
    {
        lock (Crit)
        {
            var protocols = Cache[chainId];
            if (protocols.Count == 0)
            {
                protocol = null;
                return false;
            }
            protocol = protocols[^1];
            return true;
        }
    }
}
