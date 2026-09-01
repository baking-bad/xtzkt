using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

partial class TransactionCommit
{
    public virtual async Task<(
        XEvmMichelsonTransactionOperation,
        XMichelsonAddress,
        IEnumerable<BigMapDiff>?,
        IEnumerable<TicketUpdates>?
        )> ApplyEvmMichelson(byte[] hash, JsonElement tx, JsonElement receipt, JsonElement trace, bool isDelayedOp, JsonElement content)
    {
        #region init
        var block = Context.Block;

        var senderAddress = tx.RequiredString("from");
        var sender = await Helpers.GetOrCreateXEvmUser(senderAddress);

        var gatewayAddress = tx.RequiredString("to");
        var gateway = (XEvmContract)await Cache.Addresses.GetExistingAsync(gatewayAddress);

        var aliasAddress = MichelsonRuntime.GetAlias(senderAddress);
        var alias = await Helpers.GetOrCreateXMichelsonAlias(aliasAddress, sender);

        var targetAddress = content.RequiredString("destination");
        var target = await Helpers.GetOrCreateXMichelsonAddress(targetAddress);

        var result = content.Required("result");

        var effectiveGasPrice = receipt.RequiredHexBigInteger("effectiveGasPrice");
        var gasUsed = receipt.RequiredHexInt32("gasUsed");
        var amountSent = tx.RequiredHexBigInteger("value");
        var amountReceived = content.RequiredInt64("amount");
        var roundingloss = amountSent - new BigInteger(amountReceived) * M12;
        var status = receipt.RequiredEvmOpStatus("status");
        var input = trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;

        // the gateway is a precompile with a known abi, so its parameters are never guessed
        var (gatewayEp, gatewayParams, _) = input != null
            ? await ParseParameters(gateway, input)
            : (null, null, null);

        var (entrypoint, paramsRaw, paramsJson, guessed) = target is not XMichelsonUser && content.TryGetProperty("parameters", out var parameters)
            ? await ParseParameters(target, parameters)
            : (null, null, null, null);

        var daFee = Helpers.GetDaFee(tx, isDelayedOp);
        var gasFee = Helpers.GetGasFee(effectiveGasPrice, gasUsed, daFee);

        var op = new XEvmMichelsonTransactionOperation
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
            GatewayId = gateway.Id,
            GatewayInput = input,
            GatewayEntrypoint = gatewayEp,
            GatewayParameters = gatewayParams,
            AliasId = alias.Id,
            TargetId = target.Id,
            TargetCodeHash = (target as XMichelsonContract)?.CodeHash,
            Entrypoint = entrypoint,
            Parameters = paramsJson,
            ParametersRaw = paramsRaw,
            Guessed = guessed,
            DaFee = daFee,
            GasFee = gasFee,
            AmountSent = amountSent,
            RoundingLoss = roundingloss,
            AmountReceived = amountReceived,
            Counter = tx.RequiredHexInt32("nonce"),
            GasLimit = GetGasLimit(tx),
            GasUsed = gasUsed,
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
        #endregion

        #region apply operation
        PayFee(sender, op.DaFee);
        BurnFee(sender, op.GasFee);
        sender.TransactionsCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        Db.TryAttach(gateway);
        gateway.TransactionsCount++;
        gateway.LastLevel = op.Level;
        gateway.LastTimestamp = op.Timestamp;

        Db.TryAttach(alias);
        alias.TransactionsCount++;
        alias.LastLevel = op.Level;
        alias.LastTimestamp = op.Timestamp;

        if (target != alias)
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
        IEnumerable<BigMapDiff>? bigmapDiffs = null;
        IEnumerable<TicketUpdates>? ticketUpdates = null;

        if (op.Status == OperationStatus.Applied)
        {
            // CALLCODE/DELEGATECALL are restricted for gateway calls, so we don't check OpCode here
            Spend(sender, op.AmountSent);
            Receive(target, op.AmountReceived);

            Context.Statistics.TotalLost += op.RoundingLoss;

            if (target.Hash == MichelsonRuntime.NullAddress)
                Context.Statistics.TotalBanished += new BigInteger(op.AmountReceived) * M12;

            if (result.TryGetProperty("storage", out var storage))
            {
                op.StorageId = await ProcessStorage(op.Id, target, storage);
                bigmapDiffs = ParseBigMapDiffs(result);
            }

            op.AddressRegistryIndex = await ProcessAddressRegistryDiffs(result);

            ticketUpdates = ParseTicketUpdates(result);
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return (op, target, bigmapDiffs, ticketUpdates);
    }

    public virtual async Task<(
        XEvmMichelsonTransactionOperation,
        XMichelsonAddress,
        IEnumerable<BigMapDiff>?,
        IEnumerable<TicketUpdates>?
        )> ApplyInternalEvmMichelson(IParentOperation parent, IParentOperation? cracParent, JsonElement trace, OperationStatus traceStatus, JsonElement content)
    {
        #region init
        var block = Context.Block;
        var initiator = Cache.Addresses.GetCached(parent.SenderId);

        var senderAddress = trace.RequiredString("from");
        var sender = await Helpers.GetOrCreateXEvmAddress(senderAddress);
        var senderEip7702Delegate = await GetEip7702Delegate(sender);

        var gatewayAddress = trace.RequiredString("to");
        var gateway = (XEvmContract)await Cache.Addresses.GetExistingAsync(gatewayAddress);

        XMichelsonAddress alias;
        if (sender is XEvmAlias a)
        {
            // alias of alias is expected to resolve into original owner
            alias = await Cache.Addresses.GetAsync(a.OwnerId) as XMichelsonAddress
                ?? throw new Exception("Failed to resolve alias owner");
        }
        else
        {
            var aliasAddress = MichelsonRuntime.GetAlias(senderAddress);
            alias = await Helpers.GetOrCreateXMichelsonAlias(aliasAddress, sender);
        }

        if (alias.Hash != content.RequiredString("source"))
            throw new ValidationException("Unexpected crac alias");

        var targetAddress = content.RequiredString("destination");
        var target = await Helpers.GetOrCreateXMichelsonAddress(targetAddress);

        var result = content.Required("result");

        var amountSent = trace.RequiredHexBigInteger("value");
        var amountReceived = content.RequiredInt64("amount");
        var roundingloss = amountSent - new BigInteger(amountReceived) * M12;
        var status = GetEvmTraceStatus(parent.Status, traceStatus);
        var input = trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;

        // the gateway is a precompile with a known abi, so its parameters are never guessed
        var (gatewayEp, gatewayParams, _) = input != null
            ? await ParseParameters(gateway, input)
            : (null, null, null);

        var (entrypoint, paramsRaw, paramsJson, guessed) = target is not XMichelsonUser && content.TryGetProperty("parameters", out var parameters)
            ? await ParseParameters(target, parameters)
            : (null, null, null, null);

        var op = new XEvmMichelsonTransactionOperation
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
            GatewayId = gateway.Id,
            GatewayInput = input,
            GatewayEntrypoint = gatewayEp,
            GatewayParameters = gatewayParams,
            AliasId = alias.Id,
            TargetId = target.Id,
            TargetCodeHash = (target as XMichelsonContract)?.CodeHash,
            Entrypoint = entrypoint,
            Parameters = paramsJson,
            ParametersRaw = paramsRaw,
            Guessed = guessed,
            AmountSent = amountSent,
            RoundingLoss = roundingloss,
            AmountReceived = amountReceived,
            Counter = parent.Counter,
            GasUsed = trace.RequiredHexInt32("gasUsed"),
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

        Db.TryAttach(gateway);
        gateway.TransactionsCount++;
        gateway.LastLevel = op.Level;
        gateway.LastTimestamp = op.Timestamp;

        Db.TryAttach(alias);
        alias.TransactionsCount++;
        alias.LastLevel = op.Level;
        alias.LastTimestamp = op.Timestamp;

        if (target != alias)
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
        IEnumerable<BigMapDiff>? bigmapDiffs = null;
        IEnumerable<TicketUpdates>? ticketUpdates = null;

        if (op.Status == OperationStatus.Applied)
        {
            // CALLCODE/DELEGATECALL are restricted for gateway calls, so we don't check OpCode here
            Spend(sender, op.AmountSent);
            Receive(target, op.AmountReceived);

            Context.Statistics.TotalLost += op.RoundingLoss;

            if (target.Hash == MichelsonRuntime.NullAddress)
                Context.Statistics.TotalBanished += new BigInteger(op.AmountReceived) * M12;

            if (result.TryGetProperty("storage", out var storage))
            {
                op.StorageId = await ProcessStorage(op.Id, target, storage);
                bigmapDiffs = ParseBigMapDiffs(result);
            }

            op.AddressRegistryIndex = await ProcessAddressRegistryDiffs(result);

            ticketUpdates = ParseTicketUpdates(result);
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return (op, target, bigmapDiffs, ticketUpdates);
    }

    public virtual async Task Revert(XEvmMichelsonTransactionOperation op)
    {
        #region init
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XEvmUser)!;
        var gateway = (await Cache.Addresses.GetAsync(op.GatewayId) as XEvmContract)!;
        var alias = (await Cache.Addresses.GetAsync(op.AliasId) as XMichelsonAlias)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XMichelsonAddress)!;

        Db.TryAttach(sender);
        Db.TryAttach(gateway);
        Db.TryAttach(alias);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            await RevertAddressRegistryDiffs(op.AddressRegistryIndex);

            if (op.StorageId != null)
                await RevertStorage(op.Id, (target as XMichelsonContract)!);

            // CALLCODE/DELEGATECALL are restricted for gateway calls, so we don't check OpCode here
            RevertSpend(sender, op.AmountSent);
            RevertReceive(target, op.AmountReceived);
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
        if (sender.IsEmpty()) await Helpers.RemoveXEvmUser(sender);

        Db.TryAttach(gateway);
        gateway.TransactionsCount--;
        gateway.LastLevel = op.Level;
        gateway.LastTimestamp = op.Timestamp;

        Db.TryAttach(alias);
        alias.TransactionsCount--;
        alias.LastLevel = op.Level;
        alias.LastTimestamp = op.Timestamp;
        if (alias.IsEmpty()) await Helpers.RemoveXMichelsonAlias(alias, sender);

        if (target != alias)
        {
            Db.TryAttach(target);
            target.TransactionsCount--;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
            if (target.IsEmpty()) await Helpers.RemoveXMichelsonAddress(target);
        }

        Cache.Chain.Get().TransactionOpsCount--;
        #endregion

        Cache.Chain.ReleaseOperationId();
    }

    public virtual async Task RevertInternal(XEvmMichelsonTransactionOperation op)
    {
        #region init
        var initiator = await Cache.Addresses.GetAsync(op.InitiatorId!.Value);
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XEvmAddress)!;
        var gateway = (await Cache.Addresses.GetAsync(op.GatewayId) as XEvmContract)!;
        var alias = (await Cache.Addresses.GetAsync(op.AliasId) as XMichelsonAddress)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XMichelsonAddress)!;

        Db.TryAttach(initiator);
        Db.TryAttach(sender);
        Db.TryAttach(gateway);
        Db.TryAttach(alias);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            await RevertAddressRegistryDiffs(op.AddressRegistryIndex);

            if (op.StorageId != null)
                await RevertStorage(op.Id, (target as XMichelsonContract)!);

            // CALLCODE/DELEGATECALL are restricted for gateway calls, so we don't check OpCode here
            RevertSpend(sender, op.AmountSent);
            RevertReceive(target, op.AmountReceived);
        }
        #endregion

        #region revert operation
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXEvmAddress(sender);

        Db.TryAttach(gateway);
        gateway.TransactionsCount--;
        gateway.LastLevel = op.Level;
        gateway.LastTimestamp = op.Timestamp;

        Db.TryAttach(alias);
        alias.TransactionsCount--;
        alias.LastLevel = op.Level;
        alias.LastTimestamp = op.Timestamp;
        if (alias.IsEmpty()) await Helpers.RemoveXMichelsonAddress(alias);

        if (target != alias)
        {
            Db.TryAttach(target);
            target.TransactionsCount--;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
            if (target.IsEmpty()) await Helpers.RemoveXMichelsonAddress(target);
        }

        if (initiator != sender && initiator != target)
            initiator.TransactionsCount--;

        Cache.Chain.Get().TransactionOpsCount--;
        #endregion

        Cache.Chain.ReleaseOperationId();
    }
}
