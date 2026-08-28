using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto06;
using Xtzkt.Indexers.TezosX.Protocols.Proto06.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

class Proto06Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto06Handler> logger,
    IMetrics metrics) : Proto05Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 6;
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);

    protected override Proto01.TransactionCommit TransactionCommit => new TransactionCommit(this);
}
