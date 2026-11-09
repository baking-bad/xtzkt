using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto11;
using Xtzkt.Indexers.TezosX.Protocols.Proto11.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

// Tezos X, kernel 8.0
public class Proto11Handler(
    EvmNode evmRpc,
    TezosNode michelsonRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto11Handler> logger,
    IMetrics metrics) : Proto10Handler(evmRpc, michelsonRpc, db, cache, services, config, logger, metrics)
{
    public override int Version => 11;
    public override IEvmRuntime EvmRuntime { get; } = new EvmRuntime();
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);

    protected override IActivator Activator => new ProtoActivator(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);
    IHelpers? _helpers;

    #region commits
    private protected override TransactionCommit TransactionCommit => new(this);
    private protected override LogCommit LogCommit => new(this);
    #endregion
}
