using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

partial class TransactionCommit
{
    public virtual async Task<XEvmTransactionOperation> ApplyEvm(string hash, JsonElement tx, JsonElement receipt, JsonElement trace, bool isDelayedOp)
    {
        if (tx.OptionalString("chain_id") is string chainId && chainId != Cache.Chain.Get().ChainId)
            throw new ValidationException("Invalid chain_id");

        #region init
        var block = Context.Block;

        var senderAddress = tx.RequiredString("from");
        var sender = await GetOrCreateXEvmUser(senderAddress);

        var targetAddress = trace.RequiredString("to");
        var target = await GetOrCreateXEvmAddress(targetAddress);

        var effectiveGasPrice = receipt.RequiredHexBigInteger("effectiveGasPrice");
        var gasUsed = receipt.RequiredHexInt32("gasUsed");
        var ownGasUsed = gasUsed - SubcallsGasUsed(trace);
        var fee = effectiveGasPrice * gasUsed;
        var status = receipt.RequiredEvmOpStatus("status");
        var input = trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;
        var output = trace.OptionalHexBytes("output") is byte[] _output && _output.Length > 0 ? _output : null;

        var daFee = BigInteger.Zero;
        if (!isDelayedOp)
        {
            var size = 150
                + tx.RequiredHexBytes("input").Length
                + (tx.OptionalArray("accessList")?.EnumerateArray().Sum(x => 20 + 32 * x.RequiredArray("storageKeys").Count()) ?? 0)
                + 125 * (tx.OptionalArray("authorizationList")?.Count() ?? 0);

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
            OpCode = trace.RequiredEvmOpCode("type"),
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
            Errors = status != OperationStatus.Applied
                ? trace.OptionalEscapedString("revertReason") ?? trace.OptionalEscapedString("error")
                : null,
        };

        // the authorization list is processed after the sender's nonce is incremented, but before the execution,
        // delegations are also not rolled back if the transaction fails (EIP7702)
        Db.TryAttach(sender);
        sender.Counter = op.Counter;

        if (op.OpType == EvmOpType.SetCode)
            await new Eip7702DelegationCommit(Proto).Apply(op, sender, tx.RequiredArray("authorizationList"));

        var targetEip7702Delegate = await GetEip7702Delegate(target);
        op.TargetCodeHash = ((targetEip7702Delegate ?? target) as XEvmContract)?.CodeHash;

        var (entrypoint, paramsJson, paramsGuessed) = input != null
            ? await ParseParameters(targetEip7702Delegate ?? target, input)
            : (null, null, null);

        var (resultJson, resultGuessed) = status == OperationStatus.Applied && input != null && output != null
            ? await ParseResult(targetEip7702Delegate ?? target, input, output)
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
                Spend(sender, op.Amount);
                Receive(target, op.Amount);

                if (target.Hash == EvmRuntime.NullAddress || target.Hash == EvmRuntime.DeadAddress)
                    Context.Statistics.TotalBanished += op.Amount;
            }
        }
        #endregion

        Db.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return op;
    }

    public virtual async Task<XEvmTransactionOperation> ApplyInternalEvm(IParentOperation parent, IParentOperation? cracParent, JsonElement trace, OperationStatus traceStatus)
    {
        #region init
        var block = Context.Block;
        var initiator = Cache.Addresses.GetCached(parent.SenderId);

        var senderAddress = trace.RequiredString("from");
        var sender = await GetOrCreateXEvmAddress(senderAddress);
        var senderEip7702Delegate = await GetEip7702Delegate(sender);

        var targetAddress = trace.RequiredString("to");
        var target = await GetOrCreateXEvmAddress(targetAddress);
        var targetEip7702Delegate = await GetEip7702Delegate(target);

        var status = GetEvmTraceStatus(parent.Status, traceStatus);
        var input = trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;
        var output = trace.OptionalHexBytes("output") is byte[] _output && _output.Length > 0 ? _output : null;

        var (entrypoint, paramsJson, paramsGuessed) = input != null
            ? await ParseParameters(targetEip7702Delegate ?? target, input)
            : (null, null, null);

        var (resultJson, resultGuessed) = status == OperationStatus.Applied && input != null && output != null
            ? await ParseResult(targetEip7702Delegate ?? target, input, output)
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
            SenderCodeHash = ((senderEip7702Delegate ?? sender) as XEvmContract)?.CodeHash,
            TargetId = target.Id,
            TargetCodeHash = ((targetEip7702Delegate ?? target) as XEvmContract)?.CodeHash,
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

        cracParent?.GasUsed -= MichelsonRuntime.ConvertGas(op.GasUsed);
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
                if (!IsSelfDestructWithBurn(op))
                {
                    Receive(target, op.Amount);

                    if (target.Hash == EvmRuntime.NullAddress || target.Hash == EvmRuntime.DeadAddress)
                        Context.Statistics.TotalBanished += op.Amount;
                }
                else
                {
                    Context.Statistics.TotalBurned += op.Amount;
                }
            }
        }
        #endregion

        Db.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return op;
    }

    public virtual async Task Revert(XEvmTransactionOperation op)
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
                RevertSpend(sender, op.Amount);
                RevertReceive(target, op.Amount);
            }
        }
        #endregion

        #region revert operation
        if (op.Eip7702DelegationCount > 0)
            await new Eip7702DelegationCommit(Proto).Revert(op);

        RevertPayFee(sender, op.DaFee);
        RevertBurnFee(sender, op.GasFee);
        sender.Counter = op.Counter - 1;
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await RemoveXEvmUser(sender);

        if (target != sender)
        {
            Db.TryAttach(target);
            target.TransactionsCount--;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
            if (target.IsEmpty()) await RemoveXEvmAddress(target);
        }

        Cache.Chain.Get().TransactionOpsCount--;
        #endregion

        Db.TransactionOps.Remove(op);
        Cache.Chain.ReleaseOperationId();
    }

    public virtual async Task RevertInternal(XEvmTransactionOperation op)
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
                if (!IsSelfDestructWithBurn(op))
                    RevertReceive(target, op.Amount);
            }
        }
        #endregion

        #region revert operation
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await RemoveXEvmAddress(sender);

        if (target != sender)
        {
            Db.TryAttach(target);
            target.TransactionsCount--;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
            if (target.IsEmpty()) await RemoveXEvmAddress(target);
        }

        if (initiator != sender && initiator != target)
            initiator.TransactionsCount--;

        Cache.Chain.Get().TransactionOpsCount--;
        #endregion

        Db.TransactionOps.Remove(op);
        Cache.Chain.ReleaseOperationId();
    }
}
