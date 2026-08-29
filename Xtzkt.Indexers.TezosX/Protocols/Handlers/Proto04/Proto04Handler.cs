using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto04;
using Xtzkt.Indexers.TezosX.Protocols.Proto04.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

// Calypso, kernel 3.1
class Proto04Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto04Handler> logger,
    IMetrics metrics) : Proto03Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 4;
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);
}
