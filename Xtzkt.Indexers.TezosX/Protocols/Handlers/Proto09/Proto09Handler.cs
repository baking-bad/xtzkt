using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto09;
using Xtzkt.Indexers.TezosX.Protocols.Proto09.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

// Farfadet, kernels 6.2 - 6.6
class Proto09Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto09Handler> logger,
    IMetrics metrics) : Proto08Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 9;
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);
}
