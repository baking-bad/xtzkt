using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto05;
using Xtzkt.Indexers.TezosX.Protocols.Proto05.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

class Proto05Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto05Handler> logger,
    IMetrics metrics) : Proto04Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 5;
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);
    protected override IActivator Activator => new ProtoActivator(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);

    protected override Proto01.DepositCommit DepositCommit => new DepositCommit(this);
}
