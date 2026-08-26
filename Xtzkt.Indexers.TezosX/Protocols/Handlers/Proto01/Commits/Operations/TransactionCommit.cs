using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class TransactionCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public async Task<XEvmTransactionOperation> ApplyEvm(string hash, JsonElement tx, JsonElement receipt, JsonElement trace, bool isDelayedOp)
    {
        // legacy transactions carry no chain id at all, only the typed ones do
        if (tx.OptionalString("chainId") is string chainId && chainId != Cache.Chain.Get().ChainId)
            throw new ValidationException("Invalid chainId");

        #region init
        var block = Context.Block;

        var senderAddress = tx.RequiredString("from");
        var sender = await Helpers.GetOrCreateXEvmUser(senderAddress);

        var targetAddress = tx.RequiredString("to");
        var target = await Helpers.GetOrCreateXEvmAddress(targetAddress);

        var effectiveGasPrice = receipt.RequiredHexBigInteger("effectiveGasPrice");
        var (gasUsed, ownGasUsed) = GetGasUsed(receipt, trace);
        var fee = effectiveGasPrice * gasUsed;
        var status = receipt.RequiredEvmOpStatus("status");
        var input = GetInput(tx, trace);
        var output = GetOutput(trace);

        var daFee = BigInteger.Zero;
        if (!isDelayedOp)
        {
            // the authorization list term is not charged in this era, EIP7702 arrives with Ebisu
            var size = 150
                + tx.RequiredHexBytes("input").Length
                + (tx.OptionalArray("accessList")?.EnumerateArray().Sum(x => 20 + 32 * x.RequiredArray("storageKeys").Count()) ?? 0);

            daFee = size * Context.Protocol.DaFeePerByte18;
        }
        var gasFee = fee - daFee;

        var op = new XEvmTransactionOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            OpType = receipt.RequiredEvmOpType("type"),
            OpCode = GetOpCode(trace),
            GasPrice = tx.OptionalHexBigInteger("gasPrice"),
            MaxFeePerGas = tx.OptionalHexBigInteger("maxFeePerGas"),
            MaxPriorityFeePerGas = tx.OptionalHexBigInteger("maxPriorityFeePerGas"),
            EffectiveGasPrice = effectiveGasPrice,
            SenderId = sender.Id,
            TargetId = target.Id,
            TargetCodeHash = null, // set below
            Input = input,
            Output = output,
            Entrypoint = null, // set below
            Parameters = null, // set below
            Result = null, // set below
            Guessed = null, // set below
            DaFee = daFee,
            GasFee = gasFee,
            Amount = tx.RequiredHexBigInteger("value"),
            Counter = tx.RequiredHexInt32("nonce"),
            GasLimit = tx.RequiredHexInt32("gas"),
            GasUsed = ownGasUsed,
            Status = status,
            Errors = status != OperationStatus.Applied ? GetError(trace) : null,
        };

        Db.TryAttach(sender);
        sender.Counter = op.Counter;

        op.TargetCodeHash = (target as XEvmContract)?.CodeHash;

        var (entrypoint, paramsJson, paramsGuessed) = input != null
            ? await ParseParameters(target, input)
            : (null, null, null);

        var (resultJson, resultGuessed) = status == OperationStatus.Applied && input != null && output != null
            ? await ParseResult(target, input, output)
            : (null, null);

        op.Entrypoint = entrypoint;
        op.Parameters = paramsJson;
        op.Result = resultJson;
        op.Guessed = Guessed(paramsGuessed, resultGuessed);
        #endregion

        #region apply operation
        PayFee(sender, op.DaFee);
        BurnFee(sender, op.GasFee);
        sender.TransactionsCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        if (target != sender)
        {
            Db.TryAttach(target);
            target.TransactionsCount++;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
        }

        Context.Block.Operations |= XOperations.Transaction;

        Cache.Chain.Get().TransactionOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            if (op.OpCode != EvmOpCode.CallCode &&
                op.OpCode != EvmOpCode.DelegateCall)
            {
                TransferAmount(sender, target, op.Amount);
            }
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return op;
    }

    public async Task<XEvmTransactionOperation> ApplyInternalEvm(IParentOperation parent, JsonElement trace, OperationStatus traceStatus)
    {
        #region init
        var block = Context.Block;
        var initiator = Cache.Addresses.GetCached(parent.SenderId);

        var senderAddress = trace.RequiredString("from");
        var sender = await Helpers.GetOrCreateXEvmAddress(senderAddress);

        var targetAddress = trace.RequiredString("to");
        var target = await Helpers.GetOrCreateXEvmAddress(targetAddress);

        var status = GetEvmTraceStatus(parent.Status, traceStatus);
        var input = trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;
        var output = trace.OptionalHexBytes("output") is byte[] _output && _output.Length > 0 ? _output : null;

        var (entrypoint, paramsJson, paramsGuessed) = input != null
            ? await ParseParameters(target, input)
            : (null, null, null);

        var (resultJson, resultGuessed) = status == OperationStatus.Applied && input != null && output != null
            ? await ParseResult(target, input, output)
            : (null, null);

        var op = new XEvmTransactionOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = parent.Hash,
            OpType = EvmOpType.Trace,
            OpCode = trace.RequiredEvmOpCode("type"),
            InitiatorId = initiator.Id,
            SenderId = sender.Id,
            SenderCodeHash = (sender as XEvmContract)?.CodeHash,
            TargetId = target.Id,
            TargetCodeHash = (target as XEvmContract)?.CodeHash,
            Input = input,
            Output = output,
            Entrypoint = entrypoint,
            Parameters = paramsJson,
            Result = resultJson,
            Guessed = Guessed(paramsGuessed, resultGuessed),
            Amount = trace.RequiredHexBigInteger("value"),
            Counter = parent.Counter,
            GasUsed = trace.RequiredHexInt32("gasUsed") - SubcallsGasUsed(trace),
            Status = status,
            Errors = status != OperationStatus.Applied
                ? trace.OptionalEscapedString("revertReason") ?? trace.OptionalEscapedString("error")
                : null,
        };
        #endregion

        #region apply operation
        Db.TryAttach(sender);
        sender.TransactionsCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        if (target != sender)
        {
            Db.TryAttach(target);
            target.TransactionsCount++;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
        }

        if (initiator != sender && initiator != target)
            initiator.TransactionsCount++;

        parent.InternalOperations = (parent.InternalOperations ?? 0) + 1;

        Context.Block.Operations |= XOperations.Transaction;

        Cache.Chain.Get().TransactionOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            if (op.OpCode != EvmOpCode.CallCode &&
                op.OpCode != EvmOpCode.DelegateCall)
            {
                Spend(sender, op.Amount);
                if (!(op.OpCode is EvmOpCode.SelfDestruct or EvmOpCode.Suicide && op.SenderId == op.TargetId))
                {
                    if (target.Hash != EvmRuntime.XtzBridge)
                        Receive(target, op.Amount);

                    if (target.Hash == EvmRuntime.NullAddress || target.Hash == EvmRuntime.DeadAddress)
                        Context.Statistics.TotalBanished += op.Amount;
                    else if (target.Hash == EvmRuntime.XtzBridge)
                        Context.Statistics.TotalBurned += op.Amount;
                }
                else
                {
                    Context.Statistics.TotalBurned += op.Amount;
                }
            }
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return op;
    }

    public async Task Revert(XEvmTransactionOperation op)
    {
        #region init
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XEvmUser)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XEvmAddress)!;

        Db.TryAttach(sender);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            if (op.OpCode != EvmOpCode.CallCode &&
                op.OpCode != EvmOpCode.DelegateCall)
            {
                RevertTransferAmount(sender, target, op.Amount);
            }
        }
        #endregion

        #region revert operation
        RevertPayFee(sender, op.DaFee);
        RevertBurnFee(sender, op.GasFee);
        sender.Counter = op.Counter - 1;
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXEvmUser(sender);

        if (target != sender)
        {
            Db.TryAttach(target);
            target.TransactionsCount--;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
            if (target.IsEmpty()) await Helpers.RemoveXEvmAddress(target);
        }

        Cache.Chain.Get().TransactionOpsCount--;
        #endregion

        Cache.Chain.ReleaseOperationId();
    }

    public async Task RevertInternal(XEvmTransactionOperation op)
    {
        #region init
        var initiator = await Cache.Addresses.GetAsync(op.InitiatorId!.Value);
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XEvmAddress)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XEvmAddress)!;

        Db.TryAttach(initiator);
        Db.TryAttach(sender);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            if (op.OpCode != EvmOpCode.CallCode &&
                op.OpCode != EvmOpCode.DelegateCall)
            {
                RevertSpend(sender, op.Amount);
                if (!(op.OpCode is EvmOpCode.SelfDestruct or EvmOpCode.Suicide && op.SenderId == op.TargetId))
                    if (target.Hash != EvmRuntime.XtzBridge)
                        RevertReceive(target, op.Amount);
            }
        }
        #endregion

        #region revert operation
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXEvmAddress(sender);

        if (target != sender)
        {
            Db.TryAttach(target);
            target.TransactionsCount--;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
            if (target.IsEmpty()) await Helpers.RemoveXEvmAddress(target);
        }

        if (initiator != sender && initiator != target)
            initiator.TransactionsCount--;

        Cache.Chain.Get().TransactionOpsCount--;
        #endregion

        Cache.Chain.ReleaseOperationId();
    }

    protected async Task<(string?, string?, bool?)> ParseParameters(XEvmAddress target, byte[] input)
    {
        if (target is XEvmContract contract && await Cache.Abi.GetOrDefaultAsync(contract) is Abi abi)
        {
            if (abi.TryGetFunction(input, out var fn))
            {
                try
                {
                    return (fn.Signature, AbiDecoder.DecodeToJson(input.AsSpan()[4..], fn.Inputs), false);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to parse tx inputs");
                    return (fn.Signature, null, false);
                }
            }
        }

        if (KnownSelectorsAbi.TryGetFunction(input, out var known) && (known.Inputs.Count > 0 || input.Length == 4))
        {
            try
            {
                return (known.Signature, AbiDecoder.DecodeToJson(input.AsSpan()[4..], known.Inputs), true);
            }
            catch (Exception ex)
            {
                // most likely the 4-byte selector matched by chance, and the calldata is not what
                // we guessed, so the signature is dropped as well, unlike in the abi branch above
                Logger.LogDebug(ex, "Failed to guess tx inputs");
                return (null, null, true);
            }
        }

        return (null, null, null);
    }

    protected async Task<(string?, bool?)> ParseResult(XEvmAddress target, byte[] input, byte[] output)
    {
        if (target is XEvmContract contract && await Cache.Abi.GetOrDefaultAsync(contract) is Abi abi)
        {
            if (abi.TryGetFunction(input, out var fn))
            {
                try
                {
                    return (AbiDecoder.DecodeToJson(output, fn.Outputs), false);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to parse tx outputs");
                    return (null, false);
                }
            }
        }

        if (KnownSelectorsAbi.TryGetFunction(input, out var known) && (known.Outputs.Count > 0 || output.Length == 0))
        {
            try
            {
                return (AbiDecoder.DecodeToJson(output, known.Outputs), true);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to guess tx outputs");
                return (null, true);
            }
        }

        return (null, null);
    }

    protected static bool? Guessed(bool? paramsGuessed, bool? resultGuessed)
    {
        if (paramsGuessed == null)
            return resultGuessed;

        if (resultGuessed == null)
            return paramsGuessed;

        return paramsGuessed.Value && resultGuessed.Value;
    }

    protected virtual (int GasUsed, int OwnGasUsed) GetGasUsed(JsonElement receipt, JsonElement trace)
    {
        var gasUsed = receipt.RequiredHexInt32("gasUsed");
        return (gasUsed, gasUsed);
    }

    protected virtual EvmOpCode GetOpCode(JsonElement trace)
    {
        return EvmOpCode.Call;
    }

    protected virtual byte[]? GetInput(JsonElement tx, JsonElement trace)
    {
        return tx.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;
    }

    protected virtual byte[]? GetOutput(JsonElement trace)
    {
        return null;
    }

    protected virtual string? GetError(JsonElement trace)
    {
        return null;
    }

    protected virtual void TransferAmount(XEvmUser sender, XEvmAddress target, BigInteger amount)
    {
        Spend(sender, amount);
        Receive(target, amount);

        if (target.Hash == EvmRuntime.NullAddress || target.Hash == EvmRuntime.DeadAddress)
            Context.Statistics.TotalBanished += amount;
    }

    protected virtual void RevertTransferAmount(XEvmUser sender, XEvmAddress target, BigInteger amount)
    {
        RevertSpend(sender, amount);
        RevertReceive(target, amount);
    }
}
