using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Protocols.Proto01;
using Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;
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
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);

    protected override IActivator Activator => new ProtoActivator(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);
    protected IHelpers? _helpers;

    #region commits
    protected virtual DepositCommit DepositCommit => new(this);
    protected virtual OriginationCommit OriginationCommit => new(this);
    protected virtual TransactionCommit TransactionCommit => new(this);
    protected virtual BlockCommit BlockCommit => new(this);
    protected virtual BridgeTicketsCommit BridgeTicketsCommit => new(this);
    protected virtual DepositClaimCommit DepositClaimCommit => new(this);
    protected virtual LogCommit LogCommit => new(this);
    protected virtual StateCommit StateCommit => new(this);
    protected virtual StatisticsCommit StatisticsCommit => new(this);
    protected virtual TokensCommit TokensCommit => new(this);
    #endregion

    protected override async Task Commit(MetaBlock block)
    {
        await BlockCommit.Apply(block.EvmBlock);

        foreach (var batch in block.Batches)
        {
            IParentOperation? parentOp = null;
            foreach (var operation in batch.Operations)
            {
                switch (operation)
                {
                    case EvmOperation eop when IsOrigination(eop):
                        {
                            var op = await OriginationCommit.ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed, eop.FrameGasOffset);
                            await LogCommit.ApplyEvmLogs(op, eop.Logs);
                            parentOp = op;
                            break;
                        }
                    case EvmOperation eop:
                        {
                            var op = await TransactionCommit.ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed, eop.FrameGasOffset);
                            await LogCommit.ApplyEvmLogs(op, eop.Logs);
                            parentOp = op;
                            break;
                        }
                    case EvmDeposit dop:
                        {
                            var op = await DepositCommit.ApplyEvm(batch.Hash, dop.Deposit, dop.FeederCall.Receipt);
                            await LogCommit.ApplyEvmLogs(op, dop.FeederCall.Logs);
                            parentOp = op;
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }

                foreach (var iop in operation.Internals)
                {
                    switch (iop)
                    {
                        case EvmInternalOperation eiop:
                            switch (eiop.Trace.RequiredString("type"))
                            {
                                case "CREATE":
                                case "CREATE2":
                                    {
                                        var op = await OriginationCommit.ApplyInternalEvm(parentOp, eiop.Trace, eiop.Status, eiop.ParentStatus, eiop.FrameGasOffset);
                                        await LogCommit.ApplyEvmLogs(op, eiop.Logs);
                                        break;
                                    }
                                case "CALL":
                                case "CALLCODE":
                                case "DELEGATECALL":
                                case "STATICCALL":
                                case "SELFDESTRUCT":
                                case "SUICIDE":
                                    {
                                        var op = await TransactionCommit.ApplyInternalEvm(parentOp, eiop.Trace, eiop.Status, eiop.FrameGasOffset);
                                        await LogCommit.ApplyEvmLogs(op, eiop.Logs);
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

        await TokensCommit.ApplyEvmTransfers();
        await BridgeTicketsCommit.Apply();
        await DepositClaimCommit.Apply();

        await StatisticsCommit.Apply();
        await StateCommit.Apply(block);
    }

    protected override async Task Revert()
    {
        var currBlock = await Cache.Blocks.CurrentAsync();
        Db.TryAttach(currBlock);

        await StatisticsCommit.Revert();

        await DepositClaimCommit.Revert();
        await BridgeTicketsCommit.Revert(currBlock);
        await TokensCommit.Revert(currBlock);
        await LogCommit.RevertLogs(currBlock);

        foreach (var operation in Context.EnumerateOps().OrderByDescending(x => x.Id).ToList())
        {
            switch (operation)
            {
                case XEvmDepositOperation op:
                    await DepositCommit.Revert(op);
                    break;
                case XEvmOriginationOperation op:
                    if (op.InitiatorId == null)
                        await OriginationCommit.Revert(op);
                    else
                        await OriginationCommit.RevertInternal(op);
                    break;
                case XEvmTransactionOperation op:
                    if (op.InitiatorId == null)
                        await TransactionCommit.Revert(op);
                    else
                        await TransactionCommit.RevertInternal(op);
                    break;
                default:
                    throw new NotImplementedException($"'{operation.GetType()}' is not implemented");
            }
        }

        await BlockCommit.Revert();
        await StateCommit.Revert();
    }

    protected virtual bool IsOrigination(EvmOperation op)
    {
        return op.To == null;
    }
}
