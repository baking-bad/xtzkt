using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto01;
using Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;
using Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

class Proto01Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto01Handler> logger,
    IMetrics metrics) : ProtocolHandler(db, cache, services, config, logger, metrics)
{
    public override int Version => 1;
    public override IEvmRpc EvmRpc { get; } = new EvmRpc(evmRpc);
    public override IEvmRuntime EvmRuntime { get; } = new EvmRuntime();
    public override IMichelsonRpc MichelsonRpc { get; } = new MichelsonRpc();
    public override IMichelsonRuntime MichelsonRuntime { get; } = new MichelsonRuntime();

    protected override IActivator Activator => new ProtoActivator(this);
    protected override IHelpers Helpers => new ProtoHelpers(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);

    protected override async Task Commit(IMetaBlock block)
    {
        await new BlockCommit(this).Apply(block.EvmBlock);

        foreach (var batch in block.Batches)
        {
            foreach (var operation in batch.Operations)
            {
                switch (operation.Content)
                {
                    case EvmOperation eop:
                        if (eop.To == null)
                        {
                            var op = await new OriginationCommit(this).ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, batch.Delayed);
                            await new LogCommit(this).ApplyEvmLogs(op, eop.Logs);
                        }
                        else
                        {
                            var op = await new TransactionCommit(this).ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, batch.Delayed);
                            await new LogCommit(this).ApplyEvmLogs(op, eop.Logs);
                        }
                        break;
                    case DelayedEvmDepositOperation dop:
                        {
                            var op = await new DepositCommit(this).ApplyEvm(batch.Hash, dop.Deposit, dop.FeederCall.Receipt);
                            var logCommit = new LogCommit(this);
                            await logCommit.ApplyEvmLogs(op, dop.FeederCall.Logs);
                            // shoudln't have internal ops
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        await new TokensCommit(this).ApplyEvmTransfers();

        await new StatisticsCommit(this).Apply();
        await new StateCommit(this).Apply(block);
    }

    protected override async Task Revert()
    {
        var currBlock = await Cache.Blocks.CurrentAsync();
        Db.TryAttach(currBlock);

        await new StatisticsCommit(this).Revert();

        await new TokensCommit(this).Revert(currBlock);
        await new LogCommit(this).RevertLogs(currBlock);

        foreach (var operation in Context.EnumerateOps().OrderByDescending(x => x.Id).ToList())
        {
            switch (operation)
            {
                case XEvmDepositOperation op:
                    await new DepositCommit(this).Revert(op);
                    break;
                case XEvmOriginationOperation op:
                    await new OriginationCommit(this).Revert(op);
                    break;
                case XEvmTransactionOperation op:
                    await new TransactionCommit(this).Revert(op);
                    break;
                default:
                    throw new NotImplementedException($"'{operation.GetType()}' is not implemented");
            }
        }

        await new BlockCommit(this).Revert();
        await new StateCommit(this).Revert();
    }
}
