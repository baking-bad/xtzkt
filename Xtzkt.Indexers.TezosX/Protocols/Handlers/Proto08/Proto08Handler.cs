using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto08;
using Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers;
using Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

class Proto08Handler(
    EvmNode evmRpc,
    TezosNode michelsonRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto08Handler> logger,
    IMetrics metrics) : ProtocolHandler(db, cache, services, config, logger, metrics)
{
    public override int Version => 1;
    public override IEvmRpc EvmRpc { get; } = new EvmRpc(evmRpc);
    public override IEvmRuntime EvmRuntime { get; } = new EvmRuntime();
    public override IMichelsonRpc MichelsonRpc { get; } = new MichelsonRpc(michelsonRpc);
    public override IMichelsonRuntime MichelsonRuntime { get; } = new MichelsonRuntime();

    protected override IActivator Activator => new ProtoActivator(this);
    protected override IHelpers Helpers => new ProtoHelpers(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);

    protected override async Task Commit(IMetaBlock block)
    {
        await new BlockCommit(this).Apply(block.EvmBlock);

        var bigMapCommit = new BigMapCommit(this);
        var ticketsCommit = new TicketsCommit(this);

        foreach (var batch in block.Batches)
        {
            var isFirstOp = true;
            IParentOperation? parentOp = null;
            Dictionary<IMetaContent, IParentOperation> cracOps = [];
            List<IXManagerOperation> managerOps = new(batch.Operations.Count);

            foreach (var operation in batch.Operations)
            {
                switch (operation.Content)
                {
                    case MichelsonOperation mop:
                        switch (mop.Content.RequiredString("kind"))
                        {
                            case "increase_paid_storage":
                                {
                                    var op = await new IncreasePaidStorageCommit(this).Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);
                                    managerOps.Add(op);
                                    break;
                                }
                            case "reveal":
                                {
                                    var op = await new RevealsCommit(this).Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);
                                    managerOps.Add(op);
                                    break;
                                }
                            case "register_global_constant":
                                {
                                    var op = await new RegisterConstantsCommit(this).Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);
                                    managerOps.Add(op);
                                    break;
                                }
                            case "origination":
                                {
                                    var commit = new OriginationCommit(this);
                                    await commit.Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);

                                    if (commit.BigMapDiffs != null)
                                        bigMapCommit.Append(commit.Origination, commit.Contract!, commit.BigMapDiffs);

                                    managerOps.Add(commit.Origination);
                                    break;
                                }
                            case "transaction":
                                {
                                    var (op, target, bigmapDiffs, ticketUpdates)
                                        = await new TransactionCommit(this).ApplyMichelson(batch.Hash, mop.Content, batch.Delayed, isFirstOp);

                                    if (bigmapDiffs != null)
                                        bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                                    if (ticketUpdates != null)
                                        ticketsCommit.Append(op, op, ticketUpdates);

                                    parentOp = op;
                                    managerOps.Add(op);
                                    break;
                                }
                            case "transfer_ticket":
                                {
                                    var commit = new TransferTicketCommit(this);
                                    await commit.Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);

                                    if (commit.TicketUpdates != null)
                                        ticketsCommit.Append(commit.Operation, commit.Operation, commit.TicketUpdates);

                                    parentOp = commit.Operation;
                                    managerOps.Add(commit.Operation);
                                    break;
                                }
                            default:
                                throw new NotImplementedException($"'{mop.Content.RequiredString("kind")}' is not expected in manager operations");
                        }
                        break;
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
                    case CracOperation cop when cop.GatewayCall is EvmOperation eop && cop.TargetCall is MichelsonInternalOperation miop:
                        {
                            var (op, target, bigmapDiffs, ticketUpdates)
                                = await new TransactionCommit(this).ApplyEvmMichelson(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed, miop.Content);

                            if (bigmapDiffs != null)
                                bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                            if (ticketUpdates != null)
                                ticketsCommit.Append(op, op, ticketUpdates);

                            parentOp = op;
                            cracOps.Add(operation.Content, parentOp);
                            break;
                        }
                    case CracOperation cop when cop.GatewayCall is MichelsonOperation mop && cop.TargetCall is EvmInternalOperation eiop:
                        {
                            var op = await new TransactionCommit(this).ApplyMichelsonEvm(batch.Hash, mop.Content, batch.Delayed, isFirstOp, eiop.Trace);
                            await new LogCommit(this).ApplyEvmLogs(op, eiop.Logs);
                            managerOps.Add(op);
                            parentOp = op;
                            cracOps.Add(operation.Content, parentOp);
                            break;
                        }
                    case DelayedMichelsonDepositOperation dop:
                        {
                            await new DepositCommit(this).ApplyMichelson(batch.Hash, dop.Deposit, dop.FeederCall.Content);
                            // shoudln't have internal ops
                            break;
                        }
                    case DelayedEvmDepositOperation dop:
                        {
                            var op = await new DepositCommit(this).ApplyEvm(batch.Hash, dop.Deposit, dop.FeederCall.Receipt);
                            var logCommit = new LogCommit(this);
                            await logCommit.ApplyEvmLogs(op, dop.FeederCall.Logs);
                            foreach (var bridgeCall in dop.BridgeCalls)
                                if (bridgeCall.Status == OperationStatus.Applied)
                                    await logCommit.ApplyEvmLogs(op, bridgeCall.Logs);
                            // shoudln't have internal ops
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }

                foreach (var iop in operation.Internals)
                {
                    var cracParent = iop.CracParent is IMetaContent cp ? cracOps[cp] : null;
                    switch (iop.Content)
                    {
                        case MichelsonInternalOperation miop:
                            switch (miop.Content.RequiredString("kind"))
                            {
                                case "origination":
                                    {
                                        var commit = new OriginationCommit(this);
                                        await commit.ApplyInternal(parentOp!, cracParent, miop.Content);

                                        if (commit.BigMapDiffs != null)
                                            bigMapCommit.Append(commit.Origination, commit.Contract!, commit.BigMapDiffs);
                                        
                                        break;
                                    }
                                case "transaction":
                                    {
                                        var (op, target, bigmapDiffs, ticketUpdates)
                                            = await new TransactionCommit(this).ApplyInternalMichelson(parentOp!, cracParent, miop.Content);

                                        if (bigmapDiffs != null)
                                            bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                                        if (ticketUpdates != null)
                                            ticketsCommit.Append(parentOp!, op, ticketUpdates);

                                        break;
                                    }
                                case "event":
                                    {
                                        await new LogCommit(this).ApplyMichelsonLog(cracParent, miop.Content);
                                        break;
                                    }
                                default:
                                    throw new NotImplementedException($"internal '{miop.Content.RequiredString("kind")}' is not implemented");
                            }
                            break;
                        case EvmInternalOperation eiop:
                            switch (eiop.Trace.RequiredString("type"))
                            {
                                case "CREATE":
                                case "CREATE2":
                                    {
                                        var op = await new OriginationCommit(this).ApplyInternalEvm(parentOp!, cracParent, eiop.Trace, eiop.Status, eiop.ParentStatus);
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
                                        var op = await new TransactionCommit(this).ApplyInternalEvm(parentOp!, cracParent, eiop.Trace, eiop.Status);
                                        await new LogCommit(this).ApplyEvmLogs(op, eiop.Logs);
                                        break;
                                    }
                                default:
                                    throw new NotImplementedException($"EVM trace type {eiop.Trace.RequiredString("type")} is not supported");
                            }
                            break;
                        case InternalCracOperation ciop when ciop.GatewayCall is MichelsonInternalOperation miop && ciop.TargetCall is EvmInternalOperation eiop:
                            {
                                var op = await new TransactionCommit(this).ApplyInternalMichelsonEvm(parentOp!, cracParent, miop.Content, eiop.Trace);
                                await new LogCommit(this).ApplyEvmLogs(op, eiop.Logs);
                                cracOps.Add(iop.Content, op);
                                break;
                            }
                        case InternalCracOperation ciop when ciop.GatewayCall is EvmInternalOperation eiop && ciop.TargetCall is MichelsonInternalOperation miop:
                            {
                                var (op, target, bigmapDiffs, ticketUpdates)
                                = await new TransactionCommit(this).ApplyInternalEvmMichelson(parentOp!, cracParent, eiop.Trace, eiop.Status, miop.Content);

                                if (bigmapDiffs != null)
                                    bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                                if (ticketUpdates != null)
                                    ticketsCommit.Append(parentOp!, op, ticketUpdates);

                                cracOps.Add(iop.Content, op);
                                break;
                            }
                        default:
                            throw new InvalidOperationException();
                    }
                }

                isFirstOp = false;
            }

            #region normalize michelson fees
            if (managerOps.Count > 1)
            {
                var totalGasFee = 0L;
                var totalGasRefund = 0L;
                var totalGasLimit = 0L;
                foreach (var op in managerOps)
                {
                    totalGasFee += op.GasFee;
                    totalGasRefund += op.GasRefund;
                    totalGasLimit += op.GasLimit;
                }

                if (totalGasLimit != 0)
                {
                    var sumFee = 0L;
                    var sumRefund = 0L;
                    foreach (var op in managerOps)
                    {
                        op.GasFee = totalGasFee * op.GasLimit / totalGasLimit;
                        sumFee += op.GasFee;

                        // TODO: when GasUsed is fixed, distribute GasRefund by GasUsed instead
                        op.GasRefund = totalGasRefund * op.GasLimit / totalGasLimit;
                        sumRefund += op.GasRefund;
                    }

                    managerOps[0].GasFee += totalGasFee - sumFee;
                    managerOps[0].GasRefund += totalGasRefund - sumRefund;
                }
            }
            #endregion
        }

        await bigMapCommit.Apply();
        await ticketsCommit.Apply();
        await new TokensCommit(this).Apply(bigMapCommit.Updates);
        await new TokensCommit(this).ApplyEvmTransfers();
        await new BridgeTicketsCommit(this).Apply();
        await new DepositClaimCommit(this).Apply();

        await new StatisticsCommit(this).Apply();
        await new StateCommit(this).Apply(block);
    }

    protected override async Task Revert()
    {
        var currBlock = await Cache.Blocks.CurrentAsync();
        Db.TryAttach(currBlock);

        await new StatisticsCommit(this).Revert();

        await new DepositClaimCommit(this).Revert();
        await new BridgeTicketsCommit(this).Revert(currBlock);
        await new TokensCommit(this).Revert(currBlock);
        await new TicketsCommit(this).Revert(currBlock);
        await new BigMapCommit(this).Revert(currBlock);
        await new LogCommit(this).RevertLogs(currBlock);

        foreach (var operation in Context.EnumerateOps().OrderByDescending(x => x.Id).ToList())
        {
            switch (operation)
            {
                case XMichelsonDepositOperation op:
                    await new DepositCommit(this).Revert(op);
                    break;
                case XEvmDepositOperation op:
                    await new DepositCommit(this).Revert(op);
                    break;
                case XIncreasePaidStorageOperation op:
                    await new IncreasePaidStorageCommit(this).Revert(op);
                    break;
                case XMichelsonOriginationOperation op:
                    if (op.InitiatorId == null)
                        await new OriginationCommit(this).Revert(op);
                    else
                        await new OriginationCommit(this).RevertInternal(op);
                    break;
                case XEvmOriginationOperation op:
                    if (op.InitiatorId == null)
                        await new OriginationCommit(this).Revert(op);
                    else
                        await new OriginationCommit(this).RevertInternal(op);
                    break;
                case XRegisterConstantOperation op:
                    await new RegisterConstantsCommit(this).Revert(op);
                    break;
                case XRevealOperation op:
                    await new RevealsCommit(this).Revert(op);
                    break;
                case XMichelsonTransactionOperation op:
                    if (op.InitiatorId == null)
                        await new TransactionCommit(this).Revert(op);
                    else
                        await new TransactionCommit(this).RevertInternal(op);
                    break;
                case XEvmTransactionOperation op:
                    if (op.InitiatorId == null)
                        await new TransactionCommit(this).Revert(op);
                    else
                        await new TransactionCommit(this).RevertInternal(op);
                    break;
                case XMichelsonEvmTransactionOperation op:
                    if (op.InitiatorId == null)
                        await new TransactionCommit(this).Revert(op);
                    else
                        await new TransactionCommit(this).RevertInternal(op);
                    break;
                case XEvmMichelsonTransactionOperation op:
                    if (op.InitiatorId == null)
                        await new TransactionCommit(this).Revert(op);
                    else
                        await new TransactionCommit(this).RevertInternal(op);
                    break;
                case XTransferTicketOperation op:
                    await new TransferTicketCommit(this).Revert(op);
                    break;
                default:
                    throw new NotImplementedException($"'{operation.GetType()}' is not implemented");
            }
        }

        await new BlockCommit(this).Revert();
        await new StateCommit(this).Revert();
    }

    protected override async Task CheckMichelsonActivationLevel(XChain state)
    {
        if (state.MichelsonActivationLevel is null)
        {
            var michelsonActivationLevel = await EvmRpc.GetMichelsonActivationLevel();
            state.MichelsonActivationLevel = michelsonActivationLevel.OptionalInt32();
        }
    }
}
