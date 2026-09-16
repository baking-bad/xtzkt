using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Utils.Extensions;

namespace Xtzkt.Api.Services.Cache
{
    public class ChainCache
    {
        #region cache
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

        public int Count()
        {
            return Chains.Count(x => x != null);
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
        #endregion

        #region resolvers
        public bool TryResolveChain(ChainInfoEqParameter p, [NotNullWhen(true)] out Chain? chain)
        {
            if (p.Id == null && p.ChainId == null)                
                throw new InvalidOperationException("Cannot resolve chain by empty filter"); // should never happen

            chain = Get().FirstOrDefault(x =>
                (p.Id == null || p.Id.Eq == x.Id) &&
                (p.ChainId == null || p.ChainId.Eq == x.ChainId));

            return chain != null;
        }

        public List<Chain> Resolve(ChainInfoParameter? p)
        {
            var chains = Get();

            if (p == null || p.IsEmpty())
                return chains;

            chains = [.. chains.Where(x => (p.Id == null || p.Id.Matches(x.Id)) && (p.ChainId == null || p.ChainId.Matches(x.ChainId)))];

            p.Id = chains.Count == Count()
                ? null
                : chains.Count == 1
                    ? new() { Eq = chains[0].Id }
                    : new() { In = [.. chains.Select(x => x.Id)] };
            p.ChainId = null;

            return chains;
        }

        public Int32RangeParameter? GetId16Range(List<Chain> chains)
        {
            if (chains.Count == Count())
                return null;

            if (chains.Count == 0)
                return new()
                {
                    Gt = short.MaxValue,
                    Lt = short.MinValue,
                };

            if (chains.Count == 1)
                return new()
                {
                    Ge = IdLayout.MinId16(chains[0].Id),
                    Le = IdLayout.MaxId16(chains[0].Id),
                };

            if (chains[^1].Id - chains[0].Id + 1 != chains.Count)
                throw new BadRequestException("chain", "Filtering by non-adjacent chains is not supported, query them separately");

            return new()
            {
                Ge = IdLayout.MinId16(chains[0].Id),
                Le = IdLayout.MaxId16(chains[^1].Id),
            };
        }

        public Int32RangeParameter? GetId32Range(List<Chain> chains)
        {
            if (chains.Count == Count())
                return null;

            if (chains.Count == 0)
                return new()
                {
                    Gt = int.MaxValue,
                    Lt = int.MinValue,
                };

            if (chains.Count == 1)
                return new()
                {
                    Ge = IdLayout.MinId32(chains[0].Id),
                    Le = IdLayout.MaxId32(chains[0].Id),
                };

            if (chains[^1].Id - chains[0].Id + 1 != chains.Count)
                throw new BadRequestException("chain", "Filtering by non-adjacent chains is not supported, query them separately");

            return new()
            {
                Ge = IdLayout.MinId32(chains[0].Id),
                Le = IdLayout.MaxId32(chains[^1].Id),
            };
        }

        public Int64RangeParameter? GetId64Range(List<Chain> chains)
        {
            if (chains.Count == Count())
                return null;

            if (chains.Count == 0)
                return new()
                {
                    Gt = long.MaxValue,
                    Lt = long.MinValue,
                };

            if (chains.Count == 1)
                return new()
                {
                    Ge = IdLayout.MinId64(chains[0].Id),
                    Le = IdLayout.MaxId64(chains[0].Id),
                };

            if (chains[^1].Id - chains[0].Id + 1 != chains.Count)
                throw new BadRequestException("chain", "Filtering by non-adjacent chains is not supported, query them separately");

            return new()
            {
                Ge = IdLayout.MinId64(chains[0].Id),
                Le = IdLayout.MaxId64(chains[^1].Id),
            };
        }

        public DateTimeRangeParameter? GetTimestampRange(List<Chain> chains)
        {
            if (chains.Count == Count())
                return null;

            if (chains.Count == 0)
                return new()
                {
                    Gt = DateTimeExtension.UtcMaxValue,
                    Lt = DateTimeExtension.UtcMinValue,
                };

            if (chains.Count == 1)
                return new()
                {
                    Ge = chains[0].GenesisTimestamp,
                    Le = chains[0].Timestamp.AddMinutes(5),
                };

            if (chains[^1].Id - chains[0].Id + 1 != chains.Count)
                throw new BadRequestException("chain", "Filtering by non-adjacent chains is not supported, query them separately");

            return new()
            {
                Ge = chains.Min(x => x.GenesisTimestamp),
                Le = chains.Max(x => x.Timestamp).AddMinutes(5),
            };
        }
        #endregion
    }
}
