using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto03;
using Xtzkt.Indexers.TezosX.Protocols.Proto03.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

class Proto03Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto03Handler> logger,
    IMetrics metrics) : Proto02Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 3;
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);
}
