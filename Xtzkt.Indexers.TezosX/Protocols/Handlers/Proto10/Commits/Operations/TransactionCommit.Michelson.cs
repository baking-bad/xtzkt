using System.Numerics;
using System.Text.Json;
using Netezos.Forging;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

partial class TransactionCommit
{
    public virtual async Task<(
        XMichelsonTransactionOperation,
        XMichelsonAddress,
        IEnumerable<BigMapDiff>?,
        IEnumerable<TicketUpdates>?
        )> ApplyMichelson(byte[] hash, JsonElement content, bool isDelayedOp, bool isFirstOp)
    {
        #region init
        var block = Context.Block;
        var senderAddress = content.RequiredString("source");
        var sender = await Helpers.GetOrCreateXMichelsonUser(senderAddress);

        var targetAddress = content.RequiredString("destination");
        var target = await Helpers.GetOrCreateXMichelsonAddress(targetAddress);

        var metadata = content.Required("metadata");
        var result = metadata.Required("operation_result");

        var fee = content.RequiredInt64("fee");
        var counter = content.RequiredInt32("counter");
        var gasLimit = content.RequiredInt32("gas_limit");
        var storageLimit = content.RequiredInt32("storage_limit");
        var amount = content.RequiredInt64("amount");
        var status = result.RequiredOpStatus("status");
        
        var (entrypoint, paramsRaw, paramsJson, guessed) = target is not XMichelsonUser && content.TryGetProperty("parameters", out var parameters)
            ? await ParseParameters(target, parameters)
            : (null, null, null, null);

        var daFee = 0L;
        if (!isDelayedOp)
        {
            var size = LocalForge.ForgeTransaction(new()
            {
                Source = senderAddress,
                Destination = targetAddress,
                Counter = counter,
                GasLimit = gasLimit,
                StorageLimit = storageLimit,
                Fee = fee,
                Amount = amount,
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

        var op = new XMichelsonTransactionOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            Amount = amount,
            DaFee = daFee,
            GasFee = gasFee,
            GasRefund = gasRefund,
            Counter = counter,
            GasLimit = gasLimit,
            StorageLimit = storageLimit,
            SenderId = sender.Id,
            TargetId = target.Id,
            Entrypoint = entrypoint,
            Parameters = paramsJson,
            ParametersRaw = paramsRaw,
            Guessed = guessed,
            TargetCodeHash = (target as XMichelsonContract)?.CodeHash,
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

        if (target != sender)
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
        IEnumerable<BigMapDiff>? bigmapDiffs = null;
        IEnumerable<TicketUpdates>? ticketUpdates = null;

        if (op.Status == OperationStatus.Applied)
        {
            BurnFee(sender, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            Spend(sender, op.Amount);
            Receive(target, op.Amount);

            if (result.TryGetProperty("storage", out var storage))
            {
                op.StorageId = await ProcessStorage(op.Id, target, storage);
                bigmapDiffs = ParseBigMapDiffs(result);
            }

            op.AddressRegistryIndex = await ProcessAddressRegistryDiffs(result);

            ticketUpdates = ParseTicketUpdates(result);

            if (target.Hash == MichelsonRuntime.NullAddress)
                Context.Statistics.TotalBanished += new BigInteger(op.Amount) * M12;
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return (op, target, bigmapDiffs, ticketUpdates);
    }

    public virtual async Task<(
        XMichelsonTransactionOperation,
        XMichelsonAddress,
        IEnumerable<BigMapDiff>?,
        IEnumerable<TicketUpdates>?
        )> ApplyInternalMichelson(IParentOperation parent, IParentOperation? cracParent, JsonElement content)
    {
        #region init
        var block = Context.Block;
        var initiator = Cache.Addresses.GetCached(parent.SenderId);

        var senderAddress = content.RequiredString("source");
        var sender = await Helpers.GetOrCreateXMichelsonAddress(senderAddress);

        var targetAddress = content.RequiredString("destination");
        var target = await Helpers.GetOrCreateXMichelsonAddress(targetAddress);

        var (entrypoint, paramsRaw, paramsJson, guessed) = target is not XMichelsonUser && content.TryGetProperty("parameters", out var parameters)
            ? await ParseParameters(target, parameters)
            : (null, null, null, null);

        var result = content.Required("result");

        var consumedMilligas = result.OptionalInt64("consumed_milligas") ?? 0;
        var paidStorageSizeDiff = result.OptionalInt32("paid_storage_size_diff");
        var (storageFee, allocationFee) = GetStorageFees(result, result.OptionalBool("allocated_destination_contract") == true, paidStorageSizeDiff);

        var op = new XMichelsonTransactionOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            InitiatorId = initiator.Id,
            Level = parent.Level,
            Timestamp = parent.Timestamp,
            DaFee = 0,
            GasFee = 0,
            GasRefund = 0,
            GasLimit = 0,
            StorageLimit = 0,
            Hash = parent.Hash,
            Counter = parent.Counter,
            Amount = content.RequiredInt64("amount"),
            Nonce = content.RequiredInt32("nonce"),
            SenderId = sender.Id,
            SenderCodeHash = (sender as XMichelsonContract)?.CodeHash,
            TargetId = target.Id,
            TargetCodeHash = (target as XMichelsonContract)?.CodeHash,
            Entrypoint = entrypoint,
            Parameters = paramsJson,
            ParametersRaw = paramsRaw,
            Guessed = guessed,
            Status = result.RequiredOpStatus("status"),
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

        if (target != sender)
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
        IEnumerable<BigMapDiff>? bigmapDiffs = null;
        IEnumerable<TicketUpdates>? ticketUpdates = null;

        if (op.Status == OperationStatus.Applied)
        {
            if (initiator is XMichelsonAddress _initiator)
                BurnFee(_initiator, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            Spend(sender, op.Amount);
            Receive(target, op.Amount);

            if (result.TryGetProperty("storage", out var storage))
            {
                op.StorageId = await ProcessStorage(op.Id, target, storage);
                bigmapDiffs = ParseBigMapDiffs(result);
            }

            op.AddressRegistryIndex = await ProcessAddressRegistryDiffs(result);

            ticketUpdates = ParseTicketUpdates(result);

            if (target.Hash == MichelsonRuntime.NullAddress)
                Context.Statistics.TotalBanished += new BigInteger(op.Amount) * M12;
        }
        #endregion

        Batch.TransactionOps.Add(op);
        Context.TransactionOps.Add(op);

        return (op, target, bigmapDiffs, ticketUpdates);
    }

    public virtual async Task Revert(XMichelsonTransactionOperation op)
    {
        #region entities
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XMichelsonUser)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XMichelsonAddress)!;

        Db.TryAttach(sender);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            await RevertAddressRegistryDiffs(op.AddressRegistryIndex);

            if (op.StorageId != null)
                await RevertStorage(op.Id, (target as XMichelsonContract)!);

            RevertBurnFee(sender, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            RevertSpend(sender, op.Amount);
            RevertReceive(target, op.Amount);
        }
        #endregion

        #region revert operation
        RevertPayFee(sender, op.DaFee);
        RevertBurnFee(sender, op.GasFee - op.GasRefund);
        sender.Counter = op.Counter - 1;
        sender.Revealed = true;
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXMichelsonUser(sender);

        if (target != sender)
        {
            target.TransactionsCount--;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
            if (target.IsEmpty()) await Helpers.RemoveXMichelsonAddress(target);
        }

        Cache.Chain.Get().TransactionOpsCount--;
        #endregion

        Cache.Chain.ReleaseOperationId();
    }

    public virtual async Task RevertInternal(XMichelsonTransactionOperation op)
    {
        #region entities
        var initiator = await Cache.Addresses.GetAsync(op.InitiatorId!.Value);
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XMichelsonAddress)!;
        var target = (await Cache.Addresses.GetAsync(op.TargetId) as XMichelsonAddress)!;

        Db.TryAttach(initiator);
        Db.TryAttach(sender);
        Db.TryAttach(target);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            await RevertAddressRegistryDiffs(op.AddressRegistryIndex);

            if (op.StorageId != null)
                await RevertStorage(op.Id, (target as XMichelsonContract)!);

            if (initiator is XMichelsonAddress _initiator)
                RevertBurnFee(_initiator, (op.StorageFee ?? 0) + (op.AllocationFee ?? 0));
            RevertSpend(sender, op.Amount);
            RevertReceive(target, op.Amount);
        }
        #endregion

        #region revert operation
        sender.TransactionsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXMichelsonAddress(sender);

        if (target != sender)
        {
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
