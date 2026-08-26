using System.Text.Json;
using Netezos.Forging;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

partial class TransactionCommit
{
    public virtual async Task<XMichelsonEvmTransactionOperation> ApplyMichelsonEvm(string hash, JsonElement content, bool isDelayedOp, bool isFirstOp, JsonElement trace)
    {
        #region init
        var block = Context.Block;

        var senderAddress = content.RequiredString("source");
        var sender = await Helpers.GetOrCreateXMichelsonUser(senderAddress);

        var gatewayAddress = content.RequiredString("destination");
        var gateway = (XMichelsonContract)await Cache.Addresses.GetExistingAsync(gatewayAddress);

        var aliasAddress = EvmRuntime.GetAlias(senderAddress);
        var alias = await Helpers.GetOrCreateXEvmAlias(aliasAddress, sender);

        var targetAddress = trace.RequiredString("to");
        var target = await Helpers.GetOrCreateXEvmAddress(targetAddress);
        var targetEip7702Delegate = await GetEip7702Delegate(target);

        var metadata = content.Required("metadata");
        var result = metadata.Required("operation_result");

        var fee = content.RequiredInt64("fee");
        var counter = content.RequiredInt32("counter");
        var gasLimit = content.RequiredInt32("gas_limit");
        var storageLimit = content.RequiredInt32("storage_limit");
        var amountSent = content.RequiredInt64("amount");
        var amountReceived = trace.RequiredHexBigInteger("value");
        var status = result.RequiredOpStatus("status");
        var input = trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;
        var output = trace.OptionalHexBytes("output") is byte[] _output && _output.Length > 0 ? _output : null;

        // the gateway is a michelson contract, so its parameters are decoded with the contract
        // schema and are never guessed
        var (gwEntrypoint, gwParamsRaw, gwParamsJson, _) = content.TryGetProperty("parameters", out var parameters)
            ? await ParseParameters(gateway, parameters)
            : (null, null, null, null);

        var (entrypoint, paramsJson, paramsGuessed) = input != null
            ? await ParseParameters(targetEip7702Delegate ?? target, input)
            : (null, null, null);

        var (resultJson, resultGuessed) = status == OperationStatus.Applied && input != null && output != null
            ? await ParseResult(targetEip7702Delegate ?? target, input, output)
            : (null, null);

        var daFee = 0L;
        if (!isDelayedOp)
        {
            var size = LocalForge.ForgeTransaction(new()
            {
                Source = senderAddress,
                Destination = gatewayAddress,
                Counter = counter,
                GasLimit = gasLimit,
                StorageLimit = storageLimit,
                Fee = fee,
                Amount = amountSent,
            }).Length;

            if (content.TryGetProperty("parameters", out var p))
            {
                size += LocalForge.ForgeEntrypoint(p.RequiredString("entrypoint")).Length;
                size += LocalForgeExt.SafeMichelineSize(p.Required("value"));
            }

            if (isFirstOp)
                size += 32 + (senderAddress.StartsWith("tz4") ? 96 : 64);

            daFee = size * Context.Protocol.DaFeePerByte;
        }
        var gasFee = fee - daFee;

        var gasRefundUpdate = metadata
            .OptionalArray("balance_updates")?
            .EnumerateArray()
            .FirstOrDefault(x =>
                x.RequiredString("kind") == "accumulator" &&
                x.RequiredString("category") == "block fees" &&
                x.RequiredInt64("change") < 0)
            ?? default;

        var gasRefund = gasRefundUpdate.ValueKind != JsonValueKind.Undefined
            ? -gasRefundUpdate.RequiredInt64("change")
            : 0;

        var paidStorageSizeDiff = result.OptionalInt32("paid_storage_size_diff");
        var (storageFee, allocationFee) = GetStorageFees(result, result.OptionalBool("allocated_destination_contract") == true, paidStorageSizeDiff);

        var op = new XMichelsonEvmTransactionOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            AmountSent = amountSent,
            AmountReceived = amountReceived,
            DaFee = daFee,
            GasFee = gasFee,
            GasRefund = gasRefund,
            Counter = counter,
            GasLimit = gasLimit,
            StorageLimit = storageLimit,
            SenderId = sender.Id,
            GatewayId = gateway.Id,
            GatewayEntrypoint = gwEntrypoint,
            GatewayParameters = gwParamsJson,
            GatewayParametersRaw = gwParamsRaw,
            AliasId = alias.Id,
            TargetId = target.Id,
            TargetCodeHash = ((targetEip7702Delegate ?? target) as XEvmContract)?.CodeHash,
            Input = input,
            Output = output,
            Entrypoint = entrypoint,
            Parameters = paramsJson,
            Result = resultJson,
            Guessed = Guessed(paramsGuessed, resultGuessed),
            Status = status,
            Errors = result.TryGetProperty("errors", out var errors)
                ? OperationErrors.Parse(content, errors)
                : null,
            GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
            StorageUsed = paidStorageSizeDiff ?? 0,
            StorageFee = storageFee,
            AllocationFee = allocationFee
        };
        #endregion

        #region apply operation
        Db.TryAttach(sender);
        PayFee(sender, op.DaFee);
        BurnFee(sender, op.GasFee - op.GasRefund);
        sender.Counter = op.Counter;
        sender.TransactionsCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        Db.TryAttach(gateway);
        gateway.TransactionsCount++;
        gateway.LastLevel = op.Level;
        gateway.LastTimestamp = op.Timestamp;

        Db.TryAttach(alias);
        // TODO: add separate protocol handler
        if (Context.Protocol.Hash == "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" || // v0.1
            Context.Protocol.Hash == "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a")   // v0.7
            alias.Counter++;
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

        block.Operations |= XOperations.Transaction;

        Cache.Chain.Get().TransactionOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            BurnFee(sender, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            Spend(sender, op.AmountSent);
            Receive(target!, op.AmountReceived);

            if (target!.Hash == EvmRuntime.NullAddress || target!.Hash == EvmRuntime.DeadAddress)
                Context.Statistics.TotalBanished += op.AmountReceived;
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return op;
    }

    public virtual async Task<XMichelsonEvmTransactionOperation> ApplyInternalMichelsonEvm(IParentOperation parent, IParentOperation? cracParent, JsonElement content, JsonElement trace)
    {
        #region init
        var block = Context.Block;
        var initiator = Cache.Addresses.GetCached(parent.SenderId);

        var senderAddress = content.RequiredString("source");
        var sender = await Helpers.GetOrCreateXMichelsonAddress(senderAddress);

        var gatewayAddress = content.RequiredString("destination");
        var gateway = (XMichelsonContract)await Cache.Addresses.GetExistingAsync(gatewayAddress);

        XEvmAddress alias;
        if (sender is XMichelsonAlias a)
        {
            // alias of alias is expected to resolve into original owner
            alias = await Cache.Addresses.GetAsync(a.OwnerId) as XEvmAddress
                ?? throw new Exception("Failed to resolve alias owner");
        }
        else
        {
            var aliasAddress = EvmRuntime.GetAlias(senderAddress);
            alias = await Helpers.GetOrCreateXEvmAlias(aliasAddress, sender);
        }

        if (alias.Hash != trace.RequiredString("from"))
            throw new ValidationException("Unexpected crac alias");

        var targetAddress = trace.RequiredString("to");
        var target = await Helpers.GetOrCreateXEvmAddress(targetAddress);
        var targetEip7702Delegate = await GetEip7702Delegate(target);

        var result = content.Required("result");
        var consumedMilligas = result.OptionalInt64("consumed_milligas") ?? 0;

        var amountSent = content.RequiredInt64("amount");
        var amountReceived = trace.RequiredHexBigInteger("value");
        var status = result.RequiredOpStatus("status");
        var input = trace.OptionalHexBytes("input") is byte[] _input && _input.Length > 0 ? _input : null;
        var output = trace.OptionalHexBytes("output") is byte[] _output && _output.Length > 0 ? _output : null;

        // the gateway is a michelson contract, so its parameters are decoded with the contract
        // schema and are never guessed
        var (gwEntrypoint, gwParamsRaw, gwParamsJson, _) = content.TryGetProperty("parameters", out var parameters)
            ? await ParseParameters(gateway, parameters)
            : (null, null, null, null);

        var (entrypoint, paramsJson, paramsGuessed) = input != null
            ? await ParseParameters(targetEip7702Delegate ?? target, input)
            : (null, null, null);

        var (resultJson, resultGuessed) = status == OperationStatus.Applied && input != null && output != null
            ? await ParseResult(targetEip7702Delegate ?? target, input, output)
            : (null, null);

        var paidStorageSizeDiff = result.OptionalInt32("paid_storage_size_diff");
        var (storageFee, allocationFee) = GetStorageFees(result, result.OptionalBool("allocated_destination_contract") == true, paidStorageSizeDiff);

        var op = new XMichelsonEvmTransactionOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = parent.Hash,
            AmountSent = amountSent,
            AmountReceived = amountReceived,
            DaFee = 0,
            GasFee = 0,
            GasRefund = 0,
            Counter = parent.Counter,
            Nonce = content.RequiredInt32("nonce"),
            InitiatorId = initiator.Id,
            SenderId = sender.Id,
            SenderCodeHash = (sender as XMichelsonContract)?.CodeHash,
            GatewayId = gateway.Id,
            GatewayEntrypoint = gwEntrypoint,
            GatewayParameters = gwParamsJson,
            GatewayParametersRaw = gwParamsRaw,
            AliasId = alias.Id,
            TargetId = target.Id,
            TargetCodeHash = ((targetEip7702Delegate ?? target) as XEvmContract)?.CodeHash,
            Input = input,
            Output = output,
            Entrypoint = entrypoint,
            Parameters = paramsJson,
            Result = resultJson,
            Guessed = Guessed(paramsGuessed, resultGuessed),
            Status = status,
            Errors = result.TryGetProperty("errors", out var errors)
                ? OperationErrors.Parse(content, errors)
                : null,
            GasUsed = (int)((consumedMilligas + 999) / 1000),
            StorageUsed = paidStorageSizeDiff ?? 0,
            StorageFee = storageFee,
            AllocationFee = allocationFee
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
        // TODO: add separate protocol handler
        if (Context.Protocol.Hash == "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" || // v0.1
            Context.Protocol.Hash == "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a")   // v0.7
            alias.Counter++;
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

        cracParent?.GasUsed -= EvmRuntime.ConvertGas(consumedMilligas);
        parent.InternalOperations = (parent.InternalOperations ?? 0) + 1;

        block.Operations |= XOperations.Transaction;

        Cache.Chain.Get().TransactionOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            if (initiator is XMichelsonAddress _initiator)
                BurnFee(_initiator, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            Spend(sender, op.AmountSent);
            Receive(target, op.AmountReceived);

            if (target.Hash == EvmRuntime.NullAddress || target.Hash == EvmRuntime.DeadAddress)
                Context.Statistics.TotalBanished += op.AmountReceived;
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return op;
    }

    public virtual async Task Revert(XMichelsonEvmTransactionOperation op)
    {
        #region init
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XMichelsonUser)!;
        var gateway = (await Cache.Addresses.GetAsync(op.GatewayId) as XMichelsonContract)!;
        var alias = (await Cache.Addresses.GetAsync(op.AliasId) as XEvmAlias)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XEvmAddress)!;

        Db.TryAttach(sender);
        Db.TryAttach(gateway);
        Db.TryAttach(alias);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            RevertBurnFee(sender, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            RevertSpend(sender, op.AmountSent);
            RevertReceive(target!, op.AmountReceived);
        }
        #endregion

        #region revert operation
        RevertPayFee(sender, op.DaFee);
        RevertBurnFee(sender, op.GasFee - op.GasRefund);
        sender.Counter = op.Counter - 1;
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXMichelsonUser(sender);

        Db.TryAttach(gateway);
        gateway.TransactionsCount--;
        gateway.LastLevel = op.Level;
        gateway.LastTimestamp = op.Timestamp;

        Db.TryAttach(alias);
        // TODO: add separate protocol handler
        if (Context.Protocol.Hash == "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" || // v0.1
            Context.Protocol.Hash == "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a")   // v0.7
            alias.Counter--;
        alias.TransactionsCount--;
        alias.LastLevel = op.Level;
        alias.LastTimestamp = op.Timestamp;
        if (alias.IsEmpty()) await Helpers.RemoveXEvmAlias(alias, sender);

        if (target != alias)
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

    public virtual async Task RevertInternal(XMichelsonEvmTransactionOperation op)
    {
        #region init
        var initiator = await Cache.Addresses.GetAsync(op.InitiatorId!.Value);
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XMichelsonAddress)!;
        var gateway = (await Cache.Addresses.GetAsync(op.GatewayId) as XMichelsonContract)!;
        var alias = (await Cache.Addresses.GetAsync(op.AliasId) as XEvmAddress)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XEvmAddress)!;

        Db.TryAttach(initiator);
        Db.TryAttach(sender);
        Db.TryAttach(gateway);
        Db.TryAttach(alias);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            if (initiator is XMichelsonAddress _initiator)
                RevertBurnFee(_initiator, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            RevertSpend(sender, op.AmountSent);
            RevertReceive(target!, op.AmountReceived);
        }
        #endregion

        #region revert operation
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXMichelsonAddress(sender);

        Db.TryAttach(gateway);
        gateway.TransactionsCount--;
        gateway.LastLevel = op.Level;
        gateway.LastTimestamp = op.Timestamp;

        Db.TryAttach(alias);
        // TODO: add separate protocol handler
        if (Context.Protocol.Hash == "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" || // v0.1
            Context.Protocol.Hash == "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a")   // v0.7
            alias.Counter--;
        alias.TransactionsCount--;
        alias.LastLevel = op.Level;
        alias.LastTimestamp = op.Timestamp;
        if (alias.IsEmpty()) await Helpers.RemoveXEvmAddress(alias);

        if (target != alias)
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
}
