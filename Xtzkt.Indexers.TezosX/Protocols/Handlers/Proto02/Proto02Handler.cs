using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto02;
using Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers;
using Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers.MetaBlock;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

class Proto02Handler(
    EvmNode evmRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto02Handler> logger,
    IMetrics metrics) : ProtocolHandler(db, cache, services, config, logger, metrics)
{
    public override int Version => 2;
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
            IParentOperation? parentOp = null;
            foreach (var operation in batch.Operations)
            {
                switch (operation.Content)
                {
                    case EvmOperation eop:
                        switch (eop.Trace.RequiredString("type"))
                        {
                            case "CREATE":
                            case "CREATE2":
                                {
                                    var op = await new OriginationCommit(this).ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed);
                                    await new LogCommit(this).ApplyEvmLogs(op, eop.Logs);
                                    parentOp = op;
                                    break;
                                }
                            case "CALL":
                            case "CALLCODE":
                            case "DELEGATECALL":
                            case "STATICCALL":
                            case "SELFDESTRUCT":
                            case "SUICIDE":
                                {
                                    var op = await new TransactionCommit(this).ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed);
                                    await new LogCommit(this).ApplyEvmLogs(op, eop.Logs);
                                    parentOp = op;
                                    break;
                                }
                            default:
                                throw new NotImplementedException($"EVM trace type {eop.Trace.RequiredString("type")} is not supported");
                        }
                        break;
                    case DelayedEvmDepositOperation dop:
                        {
                            var op = await new DepositCommit(this).ApplyEvm(batch.Hash, dop.Deposit, dop.FeederCall.Receipt);
                            await new LogCommit(this).ApplyEvmLogs(op, dop.FeederCall.Logs);
                            parentOp = op;
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }

                foreach (var iop in operation.Internals)
                {
                    switch (iop.Content)
                    {
                        case EvmInternalOperation eiop:
                            switch (eiop.Trace.RequiredString("type"))
                            {
                                case "CREATE":
                                case "CREATE2":
                                    {
                                        var op = await new OriginationCommit(this).ApplyInternalEvm(parentOp!, eiop.Trace, eiop.Status, eiop.ParentStatus);
                                        await new LogCommit(this).ApplyEvmLogs(op, eiop.Logs);
                                        break;
                                    }
                                case "CALL":
                                case "CALLCODE":
                                case "DELEGATECALL":
                                case "STATICCALL":
                                case "SELFDESTRUCT":
                                case "SUICIDE":
                                    {
                                        var op = await new TransactionCommit(this).ApplyInternalEvm(parentOp!, eiop.Trace, eiop.Status);
                                        await new LogCommit(this).ApplyEvmLogs(op, eiop.Logs);
                                        break;
                                    }
                                default:
                                    throw new NotImplementedException($"EVM trace type {eiop.Trace.RequiredString("type")} is not supported");
                            }
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
        }

        await new TokensCommit(this).ApplyEvmTransfers();
        await new BridgeTicketsCommit(this).Apply();

        await new StatisticsCommit(this).Apply();
        await new StateCommit(this).Apply(block);
    }

    protected override async Task Revert()
    {
        var currBlock = await Cache.Blocks.CurrentAsync();
        Db.TryAttach(currBlock);

        await new StatisticsCommit(this).Revert();

        await new BridgeTicketsCommit(this).Revert(currBlock);
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
                    if (op.InitiatorId == null)
                        await new OriginationCommit(this).Revert(op);
                    else
                        await new OriginationCommit(this).RevertInternal(op);
                    break;
                case XEvmTransactionOperation op:
                    if (op.InitiatorId == null)
                        await new TransactionCommit(this).Revert(op);
                    else
                        await new TransactionCommit(this).RevertInternal(op);
                    break;
                default:
                    throw new NotImplementedException($"'{operation.GetType()}' is not implemented");
            }
        }

        await new BlockCommit(this).Revert();
        await new StateCommit(this).Revert();
    }
}
