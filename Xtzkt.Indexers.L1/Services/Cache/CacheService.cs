using Xtzkt.Data;
using Xtzkt.Indexers.Common.Cache;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.L1.Services
{
    public class CacheService
    {
        public L1ChainCache Chain { get; }
        public AddressesCache Addresses { get; }
        public BakerCyclesCache BakerCycles { get; }
        public BakingRightsCache BakingRights { get; }
        public BigMapKeysCache BigMapKeys { get; }
        public BigMapsCache BigMaps { get; }
        public BlocksCache Blocks { get; }
        public PeriodsCache Periods { get; }
        public ProposalsCache Proposals { get; }
        public L1ProtocolsCache Protocols { get; }
        public RefutationGameCache RefutationGames { get; }
        public SchemasCache Schemas { get; }
        public SmartRollupCommitmentCache SmartRollupCommitments { get; }
        public SmartRollupStakesCache SmartRollupStakes { get; }
        public SoftwareCache Software { get; }
        public StakerCyclesCache StakerCycles { get; }
        public L1StatisticsCache Statistics { get; }
        public StoragesCache Storages { get; }
        public TicketBalancesCache TicketBalances { get; }
        public TicketsCache Tickets { get; }
        public TokenBalancesCache TokenBalances { get; }
        public TokensCache Tokens { get; }
        public UnstakeRequestsCache UnstakeRequests { get; }

        public CacheService(XtzktContext db, IConfiguration config)
        {
            var chain = config.GetChainConfig();
            Chain = new(db, config);
            Addresses = new(db, Chain, chain);
            BakerCycles = new(db, chain);
            BakingRights = new(db, chain);
            BigMapKeys = new(db, chain);
            BigMaps = new(db, chain);
            Blocks = new(db, Chain, chain);
            Periods = new(db, chain);
            Proposals = new(db, chain);
            Protocols = new(db, chain);
            RefutationGames = new(db);
            Schemas = new(db);
            SmartRollupCommitments = new(db);
            SmartRollupStakes = new(db);
            Software = new(db, chain);
            StakerCycles = new(db, chain);
            Statistics = new(db, chain);
            Storages = new(db);
            TicketBalances = new(db, chain);
            Tickets = new(db, chain);
            TokenBalances = new(db, chain);
            Tokens = new(db, chain);
            UnstakeRequests = new(db);
        }

        public async Task ResetAsync()
        {
            await Addresses.ResetAsync();
            await Chain.ResetAsync();
            BakerCycles.Reset();
            BakingRights.Reset();
            BigMapKeys.Reset();
            BigMaps.Reset();
            Blocks.Reset();
            Periods.Reset();
            Proposals.Reset();
            await Protocols.ResetAsync();
            RefutationGames.Reset();
            Schemas.Reset();
            SmartRollupCommitments.Reset();
            SmartRollupStakes.Reset();
            Software.Reset();
            StakerCycles.Reset();
            await Statistics.ResetAsync();
            Storages.Reset();
            TicketBalances.Reset();
            Tickets.Reset();
            TokenBalances.Reset();
            Tokens.Reset();
            UnstakeRequests.Reset();
        }

        public void Trim()
        {
            Addresses.Trim();
            BigMapKeys.Trim();
            BigMaps.Trim();
            Blocks.Trim();
            Periods.Trim();
            Proposals.Trim();
            RefutationGames.Trim();
            Schemas.Trim();
            SmartRollupCommitments.Trim();
            SmartRollupStakes.Trim();
            Software.Trim();
            StakerCycles.Trim();
            Storages.Trim();
            TicketBalances.Trim();
            Tickets.Trim();
            TokenBalances.Trim();
            Tokens.Trim();
            UnstakeRequests.Trim();
        }
    }

    public static class CacheServiceExt
    {
        public static void AddCache(this IServiceCollection services, IConfiguration config)
        {
            var cacheConfig = config.GetCacheConfig();
            AddressesCache.Configure(cacheConfig.Addresses);
            BigMapKeysCache.Configure(cacheConfig.BigMapKeys);
            BigMapsCache.Configure(cacheConfig.BigMaps);
            BlocksCache.Configure(cacheConfig.Blocks);
            PeriodsCache.Configure(cacheConfig.Periods);
            ProposalsCache.Configure(cacheConfig.Proposals);
            RefutationGameCache.Configure(cacheConfig.RefutationGames);
            SchemasCache.Configure(cacheConfig.Schemas);
            SmartRollupCommitmentCache.Configure(cacheConfig.SmartRollupCommitments);
            SmartRollupStakesCache.Configure(cacheConfig.SmartRollupStakes);
            SoftwareCache.Configure(cacheConfig.Software);
            StakerCyclesCache.Configure(cacheConfig.StakerCycles);
            StoragesCache.Configure(cacheConfig.Storages);
            TicketBalancesCache.Configure(cacheConfig.TicketBalances);
            TicketsCache.Configure(cacheConfig.Tickets);
            TokenBalancesCache.Configure(cacheConfig.TokenBalances);
            TokensCache.Configure(cacheConfig.Tokens);
            UnstakeRequestsCache.Configure(cacheConfig.UnstakeRequests);

            services.AddScoped<CacheService>();
        }
    }
}
