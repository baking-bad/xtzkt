using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Proto08;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

// Farfadet, kernel 6.1
class Proto08Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto08Handler> logger,
    IMetrics metrics) : Proto07Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 8;

    protected override IActivator Activator => new ProtoActivator(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);

    protected override Proto01.DepositCommit DepositCommit => new DepositCommit(this);
}
