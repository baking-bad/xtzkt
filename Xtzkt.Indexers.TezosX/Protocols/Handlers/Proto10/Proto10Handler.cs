using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Protocols.Proto10;
using Xtzkt.Indexers.TezosX.Protocols.Proto10.Helpers;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols;

// Ganesha, kernels 7.0 - 7.3
public class Proto10Handler(
    EvmNode evmRpc,
    TezosNode michelsonRpc,
    XtzktContext db,
    CacheService cache,
    IServiceProvider services,
    IConfiguration config,
    ILogger<Proto10Handler> logger,
    IMetrics metrics) : ProtocolHandler(db, cache, services, config, logger, metrics)
{
    public override int Version => 10;
    public override IEvmRpc EvmRpc { get; } = new EvmRpc(evmRpc);
    public override IEvmRuntime EvmRuntime { get; } = new EvmRuntime();
    public override IMichelsonRpc MichelsonRpc { get; } = new MichelsonRpc(michelsonRpc);
    public override IMichelsonRuntime MichelsonRuntime { get; } = new MichelsonRuntime();
    public override IHelpers Helpers => _helpers ??= new ProtoHelpers(this);

    protected override IActivator Activator => new ProtoActivator(this);
    protected override IMigrator Migrator => new ProtoMigrator(this);
    IHelpers? _helpers;

    #region commits
    private protected virtual DepositCommit DepositCommit => new(this);
    private protected virtual IncreasePaidStorageCommit IncreasePaidStorageCommit => new(this);
    private protected virtual OriginationCommit OriginationCommit => new(this);
    private protected virtual RegisterConstantsCommit RegisterConstantsCommit => new(this);
    private protected virtual RevealsCommit RevealsCommit => new(this);
    private protected virtual TransactionCommit TransactionCommit => new(this);
    private protected virtual TransferTicketCommit TransferTicketCommit => new(this);
    private protected virtual BigMapCommit BigMapCommit => new(this);
    private protected virtual BlockCommit BlockCommit => new(this);
    private protected virtual BridgeTicketsCommit BridgeTicketsCommit => new(this);
    private protected virtual DepositClaimCommit DepositClaimCommit => new(this);
    private protected virtual LogCommit LogCommit => new(this);
    private protected virtual StateCommit StateCommit => new(this);
    private protected virtual StatisticsCommit StatisticsCommit => new(this);
    private protected virtual TicketsCommit TicketsCommit => new(this);
    private protected virtual TokensCommit TokensCommit => new(this);
    #endregion

    protected override async Task Commit(MetaBlock block)
    {
        await BlockCommit.Apply(block.EvmBlock);

        var bigMapCommit = BigMapCommit;
        var ticketsCommit = TicketsCommit;

        foreach (var batch in block.Batches)
        {
            var isFirstOp = true;
            IParentOperation? parentOp = null;
            Dictionary<MetaContent, IParentOperation> cracOps = [];
            List<(IXManagerOperation Op, long GasUsed)> managerOps = new(batch.Operations.Count);

            foreach (var operation in batch.Operations)
            {
                var evmGas0 = Context.Block.EvmGasUsed;
                var michelsonGas0 = Context.Block.MichelsonGasUsed;
                IXManagerOperation? managerOp = null;

                switch (operation)
                {
                    case MichelsonOperation mop:
                        switch (mop.Content.RequiredString("kind"))
                        {
                            case "increase_paid_storage":
                                {
                                    var op = await IncreasePaidStorageCommit.Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);
                                    managerOp = op;
                                    break;
                                }
                            case "reveal":
                                {
                                    var op = await RevealsCommit.Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);
                                    managerOp = op;
                                    break;
                                }
                            case "register_global_constant":
                                {
                                    var op = await RegisterConstantsCommit.Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);
                                    managerOp = op;
                                    break;
                                }
                            case "origination":
                                {
                                    var commit = OriginationCommit;
                                    await commit.Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);

                                    if (commit.BigMapDiffs != null)
                                        bigMapCommit.Append(commit.Origination, commit.Contract!, commit.BigMapDiffs);

                                    managerOp = commit.Origination;
                                    break;
                                }
                            case "transaction":
                                {
                                    var (op, target, bigmapDiffs, ticketUpdates)
                                        = await TransactionCommit.ApplyMichelson(batch.Hash, mop.Content, batch.Delayed, isFirstOp);

                                    if (bigmapDiffs != null)
                                        bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                                    if (ticketUpdates != null)
                                        ticketsCommit.Append(op, op, ticketUpdates);

                                    parentOp = op;
                                    managerOp = op;
                                    break;
                                }
                            case "transfer_ticket":
                                {
                                    var commit = TransferTicketCommit;
                                    await commit.Apply(batch.Hash, mop.Content, batch.Delayed, isFirstOp);

                                    if (commit.TicketUpdates != null)
                                        ticketsCommit.Append(commit.Operation, commit.Operation, commit.TicketUpdates);

                                    parentOp = commit.Operation;
                                    managerOp = commit.Operation;
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
                                    var op = await OriginationCommit.ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed);
                                    await LogCommit.ApplyEvmLogs(op, eop.Logs);
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
                                    var op = await TransactionCommit.ApplyEvm(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed);
                                    await LogCommit.ApplyEvmLogs(op, eop.Logs);
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
                                = await TransactionCommit.ApplyEvmMichelson(batch.Hash, eop.Tx, eop.Receipt, eop.Trace, batch.Delayed, miop.Content);

                            if (bigmapDiffs != null)
                                bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                            if (ticketUpdates != null)
                                ticketsCommit.Append(op, op, ticketUpdates);

                            parentOp = op;
                            cracOps.Add(operation, parentOp);
                            break;
                        }
                    case CracOperation cop when cop.GatewayCall is MichelsonOperation mop && cop.TargetCall is EvmInternalOperation eiop:
                        {
                            var op = await TransactionCommit.ApplyMichelsonEvm(batch.Hash, mop.Content, batch.Delayed, isFirstOp, eiop.Trace);
                            await LogCommit.ApplyEvmLogs(op, eiop.Logs);
                            parentOp = op;
                            managerOp = op;
                            cracOps.Add(operation, parentOp);
                            break;
                        }
                    case MichelsonDeposit dop:
                        {
                            await DepositCommit.ApplyMichelson(batch.Hash, dop.Deposit, dop.FeederCall.Content);
                            // shoudln't have internal ops
                            break;
                        }
                    case EvmDeposit dop:
                        {
                            var op = await DepositCommit.ApplyEvm(batch.Hash, dop.Deposit, dop.FeederCall.Receipt);
                            var logCommit = LogCommit;
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
                    var cracParent = iop.CracParent is MetaContent cp ? cracOps[cp] : null;
                    switch (iop)
                    {
                        case MichelsonInternalOperation miop:
                            switch (miop.Content.RequiredString("kind"))
                            {
                                case "origination":
                                    {
                                        var commit = OriginationCommit;
                                        await commit.ApplyInternal(parentOp!, cracParent, miop.Content);

                                        if (commit.BigMapDiffs != null)
                                            bigMapCommit.Append(commit.Origination, commit.Contract!, commit.BigMapDiffs);

                                        break;
                                    }
                                case "transaction":
                                    {
                                        var (op, target, bigmapDiffs, ticketUpdates)
                                            = await TransactionCommit.ApplyInternalMichelson(parentOp!, cracParent, miop.Content);

                                        if (bigmapDiffs != null)
                                            bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                                        if (ticketUpdates != null)
                                            ticketsCommit.Append(parentOp!, op, ticketUpdates);

                                        break;
                                    }
                                case "event":
                                    {
                                        await LogCommit.ApplyMichelsonLog(cracParent, miop.Content);
                                        break;
                                    }
                                default:
                                    throw new NotImplementedException($"internal '{miop.Content.RequiredString("kind")}' is not implemented");
                            }
                            break;
                        case EvmInternalOperation eiop when CanSkip(eiop.StaticRootStatus):
                            if (eiop.Logs.Count != 0) throw new Exception("Unexpected logs in a static call");
                            break;
                        case EvmInternalOperation eiop:
                            switch (eiop.Trace.RequiredString("type"))
                            {
                                case "CREATE":
                                case "CREATE2":
                                    {
                                        var op = await OriginationCommit.ApplyInternalEvm(parentOp!, cracParent, eiop.Trace, eiop.Status, eiop.ParentStatus);
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
                                        var op = await TransactionCommit.ApplyInternalEvm(parentOp!, cracParent, eiop.Trace, eiop.Status);
                                        await LogCommit.ApplyEvmLogs(op, eiop.Logs);
                                        break;
                                    }
                                default:
                                    throw new NotImplementedException($"EVM trace type {eiop.Trace.RequiredString("type")} is not supported");
                            }
                            break;
                        case InternalCracOperation ciop when ciop.GatewayCall is MichelsonInternalOperation miop && ciop.TargetCall is EvmInternalOperation eiop:
                            {
                                var op = await TransactionCommit.ApplyInternalMichelsonEvm(parentOp!, cracParent, miop.Content, eiop.Trace);
                                await LogCommit.ApplyEvmLogs(op, eiop.Logs);
                                cracOps.Add(iop, op);
                                break;
                            }
                        case InternalCracOperation ciop when ciop.GatewayCall is EvmInternalOperation eiop && ciop.TargetCall is MichelsonInternalOperation miop:
                            {
                                var (op, target, bigmapDiffs, ticketUpdates)
                                = await TransactionCommit.ApplyInternalEvmMichelson(parentOp!, cracParent, eiop.Trace, eiop.Status, miop.Content);

                                if (bigmapDiffs != null)
                                    bigMapCommit.Append(op, (target as XMichelsonContract)!, bigmapDiffs);

                                if (ticketUpdates != null)
                                    ticketsCommit.Append(parentOp!, op, ticketUpdates);

                                cracOps.Add(iop, op);
                                break;
                            }
                        default:
                            throw new InvalidOperationException();
                    }
                }

                isFirstOp = false;
                if (managerOp != null)
                {
                    var evmGas = Context.Block.EvmGasUsed - evmGas0;
                    var michelsonGas = Context.Block.MichelsonGasUsed - michelsonGas0;

                    // note: gasUsed can be underestimated due to evm gas conversion rounding
                    var gasUsed = michelsonGas + MichelsonRuntime.ConvertGas((int)evmGas);
                    // and/or overestimated due to milligas rounding
                    gasUsed = Math.Min(gasUsed, managerOp.GasLimit!.Value);

                    #region debug
                    if (gasUsed < 0)
                        throw new Exception("Gas used cannot be negative");
                    #endregion

                    managerOps.Add((managerOp, gasUsed));
                }
            }

            #region normalize michelson fees
            if (managerOps.Count > 1)
            {
                var totalGasFee = 0L;
                var totalGasFeeRefunded = 0L;
                var totalGasLimit = 0L;
                var totalGasUsed = 0L;
                foreach (var (op, gasUsed) in managerOps)
                {
                    totalGasFee += op.GasFee!.Value;
                    totalGasFeeRefunded += op.GasFeeRefunded!.Value;
                    totalGasLimit += op.GasLimit!.Value;
                    totalGasUsed += gasUsed;
                }
                var actualFee = totalGasFee - totalGasFeeRefunded;

                if (totalGasLimit != 0)
                {
                    var sumFee = 0L;
                    foreach (var (op, _) in managerOps)
                    {
                        op.GasFee = totalGasFee * op.GasLimit!.Value / totalGasLimit;
                        sumFee += op.GasFee.Value;
                    }
                    managerOps[0].Op.GasFee += totalGasFee - sumFee;
                }

                if (totalGasUsed != 0)
                {
                    var sumRefund = 0L;
                    foreach (var (op, gasUsed) in managerOps)
                    {
                        op.GasFeeRefunded = Math.Max(0, op.GasFee!.Value - actualFee * gasUsed / totalGasUsed);
                        sumRefund += op.GasFeeRefunded.Value;
                    }
                    var refundRem = totalGasFeeRefunded - sumRefund;
                    if (refundRem >= 0)
                    {
                        managerOps[0].Op.GasFeeRefunded += refundRem;
                    }
                    else // rounding can hand out more than the batch was refunded
                    {
                        foreach (var (op, _) in managerOps)
                        {
                            var take = Math.Min(-refundRem, op.GasFeeRefunded!.Value);
                            op.GasFeeRefunded -= take;
                            refundRem += take;
                            if (refundRem == 0) break;
                        }

                        if (refundRem != 0)
                            throw new Exception("Refund distribution math is broken");
                    }
                }
            }
            #endregion
        }

        await bigMapCommit.Apply();
        await ticketsCommit.Apply();
        await TokensCommit.Apply(bigMapCommit.Updates);
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
        await TicketsCommit.Revert(currBlock);
        await BigMapCommit.Revert(currBlock);
        await LogCommit.RevertLogs(currBlock);

        foreach (var operation in Context.EnumerateOps().OrderByDescending(x => x.Id).ToList())
        {
            switch (operation)
            {
                case XMichelsonDepositOperation op:
                    await DepositCommit.Revert(op);
                    break;
                case XEvmDepositOperation op:
                    await DepositCommit.Revert(op);
                    break;
                case XIncreasePaidStorageOperation op:
                    await IncreasePaidStorageCommit.Revert(op);
                    break;
                case XMichelsonOriginationOperation op:
                    if (op.InitiatorId == null)
                        await OriginationCommit.Revert(op);
                    else
                        await OriginationCommit.RevertInternal(op);
                    break;
                case XEvmOriginationOperation op:
                    if (op.InitiatorId == null)
                        await OriginationCommit.Revert(op);
                    else
                        await OriginationCommit.RevertInternal(op);
                    break;
                case XRegisterConstantOperation op:
                    await RegisterConstantsCommit.Revert(op);
                    break;
                case XRevealOperation op:
                    await RevealsCommit.Revert(op);
                    break;
                case XMichelsonTransactionOperation op:
                    if (op.InitiatorId == null)
                        await TransactionCommit.Revert(op);
                    else
                        await TransactionCommit.RevertInternal(op);
                    break;
                case XEvmTransactionOperation op:
                    if (op.InitiatorId == null)
                        await TransactionCommit.Revert(op);
                    else
                        await TransactionCommit.RevertInternal(op);
                    break;
                case XMichelsonEvmTransactionOperation op:
                    if (op.InitiatorId == null)
                        await TransactionCommit.Revert(op);
                    else
                        await TransactionCommit.RevertInternal(op);
                    break;
                case XEvmMichelsonTransactionOperation op:
                    if (op.InitiatorId == null)
                        await TransactionCommit.Revert(op);
                    else
                        await TransactionCommit.RevertInternal(op);
                    break;
                case XTransferTicketOperation op:
                    await TransferTicketCommit.Revert(op);
                    break;
                default:
                    throw new NotImplementedException($"'{operation.GetType()}' is not implemented");
            }
        }

        await BlockCommit.Revert();
        await StateCommit.Revert();
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
