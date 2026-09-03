using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Protocols.Proto02;
using Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

// Bifrost, kernel 2.0
class Proto02Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto02Handler> logger,
    IMetrics metrics) : Proto01Handler(evmRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 2;
    public override IEvmRpc EvmRpc { get; } = new EvmRpc(evmRpc);
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);

    protected override IActivator Activator => new ProtoActivator(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);

    protected override Proto01.BlockCommit BlockCommit => new BlockCommit(this);
    protected override Proto01.LogCommit LogCommit => new LogCommit(this);
    protected override Proto01.OriginationCommit OriginationCommit => new OriginationCommit(this);
    protected override Proto01.TokensCommit TokensCommit => new TokensCommit(this);
    protected override Proto01.TransactionCommit TransactionCommit => new TransactionCommit(this);

    protected override bool IsOrigination(EvmOperation op)
    {
        return op.Trace.GetProperty("type").ValueEquals("CREATE");
    }
}
