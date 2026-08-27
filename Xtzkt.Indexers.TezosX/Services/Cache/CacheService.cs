using Xtzkt.Data;
using Xtzkt.Indexers.Common.Cache;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX.Services.Cache;

namespace Xtzkt.Indexers.TezosX.Services
{
    public class CacheService
    {
        public XChainCache Chain { get; }
        public XAddressesCache Addresses { get; }
        public BigMapKeysCache BigMapKeys { get; }
        public BigMapsCache BigMaps { get; }
        public XBlocksCache Blocks { get; }
        public BridgeTicketBalancesCache BridgeTicketBalances { get; }
        public BridgeTicketsCache BridgeTickets { get; }
        public DelayedTransactionsCache DelayedTransactions { get; }
        public XProtocolsCache Protocols { get; }
        public AbiCache Abi { get; }
        public SchemasCache Schemas { get; }
        public StakerCyclesCache StakerCycles { get; }
        public XStatisticsCache Statistics { get; }
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
            BigMapKeys = new(db, chain);
            BigMaps = new(db, chain);
            Blocks = new(db, Chain, chain);
            BridgeTicketBalances = new(db);
            BridgeTickets = new(db, chain);
            DelayedTransactions = new();
            Protocols = new(db, chain);
            Abi = new(db);
            Schemas = new(db);
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
            Addresses.Reset();
            await Chain.ResetAsync();
            BigMapKeys.Reset();
            BigMaps.Reset();
            Blocks.Reset();
            BridgeTicketBalances.Reset();
            BridgeTickets.Reset();
            DelayedTransactions.Reset();
            await Protocols.ResetAsync();
            Abi.Reset();
            Schemas.Reset();
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
            BridgeTicketBalances.Trim();
            BridgeTickets.Trim();
            DelayedTransactions.Trim();
            Abi.Trim();
            Schemas.Trim();
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
            AbiCache.Configure(cacheConfig.Abi);
            XAddressesCache.Configure(cacheConfig.Addresses);
            BigMapKeysCache.Configure(cacheConfig.BigMapKeys);
            BigMapsCache.Configure(cacheConfig.BigMaps);
            XBlocksCache.Configure(cacheConfig.Blocks);
            BridgeTicketBalancesCache.Configure(cacheConfig.BridgeTicketBalances);
            BridgeTicketsCache.Configure(cacheConfig.BridgeTickets);
            DelayedTransactionsCache.Configure(cacheConfig.DelayedTransactions);
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
