using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto07;
using Xtzkt.Indexers.TezosX.Protocols.Proto07.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

// Farfadet, kernel 6.0
class Proto07Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto07Handler> logger,
    IMetrics metrics) : Proto06Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 7;
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);
    protected override IActivator Activator => new ProtoActivator(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);
}
