using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache
{
    public class ChainCache
    {
        readonly Chain?[] Chains = new Chain?[8];
        readonly IDbContextFactory<XtzktContext> DbFactory;
        readonly ILogger Logger;

        public ChainCache(IDbContextFactory<XtzktContext> dbFactory, ILogger<ChainCache> logger)
        {
            DbFactory = dbFactory;
            Logger = logger;

            Logger.LogDebug("Initializing chain cache...");
            
            using var db = DbFactory.CreateDbContext();
            foreach (var chain in db.Chains.ToList())
                Chains[chain.Id] = chain;

            Logger.LogInformation("Chain cache initialized with {cnt} items", Chains.Count(x => x != null));
        }

        public async Task OnStateChanged(int chainId)
        {
            using var db = DbFactory.CreateDbContext();
            Chains[chainId] = await db.Chains.FirstOrDefaultAsync(x => x.Id == chainId);

            Logger.LogDebug("Updated state for chain #{chainId}", chainId);
        }

        public void OnSyncStateChanged(int chainId, int knownLevel, DateTime syncedAt)
        {
            if (Chains[chainId] is Chain chain)
            {
                chain.KnownLevel = knownLevel;
                chain.SyncedAt = syncedAt;
            }
        }

        public Models.ChainInfo GetInfo(int chainId)
        {
            var chain = Get(chainId);
            return new()
            {
                Id = chain.Id,
                ChainId = chain.ChainId,
                Layer = Models.Enums.Layers.ToString((int)chain.Layer),
            };
        }

        public List<Chain> Get()
        {
            var chains = new List<Chain>(2);
            for (int i = 0; i < Chains.Length; i++)
                if (Chains[i] is Chain chain)
                    chains.Add(chain);
            return chains;
        }

        public Chain Get(int chainId)
        {
            if (Chains[chainId] is not Chain chain)
            {
                // should never get here, but still...
                Logger.LogWarning("Inconsistent cache");
                using var db = DbFactory.CreateDbContext();
                chain = db.Chains.First(x => x.Id == chainId);
            }
            return chain;
        }
    }
}
