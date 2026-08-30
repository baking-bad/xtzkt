using Netezos.Forging;
using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class IncreasePaidStorageCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public virtual async Task<XIncreasePaidStorageOperation> Apply(string hash, JsonElement content, bool isDelayedOp, bool isFirstOp)
    {
        #region init
        var block = Context.Block;

        var senderAddress = content.RequiredString("source");
        var sender = await Helpers.GetOrCreateXMichelsonUser(senderAddress);

        var contractAddress = content.RequiredString("destination");
        var contract = await Helpers.GetOrCreateXMichelsonAddress(contractAddress);

        var metadata = content.Required("metadata");
        var result = metadata.Required("operation_result");

        var fee = content.RequiredInt64("fee");
        var counter = content.RequiredInt32("counter");
        var gasLimit = content.RequiredInt32("gas_limit");
        var storageLimit = content.RequiredInt32("storage_limit");
        var amount = BigInteger.Parse(content.RequiredString("amount"));
        var status = result.RequiredOpStatus("status");

        var daFee = 0L;
        if (!isDelayedOp)
        {
            var size = LocalForge.ForgeIncreasePaidStorage(new()
            {
                Source = sender.Hash,
                Destination = contractAddress,
                Counter = counter,
                GasLimit = gasLimit,
                StorageLimit = storageLimit,
                Fee = fee,
                Amount = amount,
            }).Length;

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

        var (storageFee, _) = GetStorageFees(result, false);

        var op = new XIncreasePaidStorageOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            DaFee = daFee,
            GasFee = gasFee,
            GasRefund = gasRefund,
            Counter = counter,
            GasLimit = gasLimit,
            StorageLimit = storageLimit,
            SenderId = sender.Id,
            ContractId = contract.Id,
            Amount = amount,
            Status = status,
            Errors = result.TryGetProperty("errors", out var errors)
                ? OperationErrors.Parse(content, errors)
                : null,
            GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
            StorageUsed = (int)((storageFee ?? 0) / Context.Protocol.ByteCost),
            StorageFee = storageFee,
        };
        #endregion

        #region apply operation
        Db.TryAttach(sender);
        PayFee(sender, op.DaFee);
        BurnFee(sender, op.GasFee - op.GasRefund);
        sender.Counter = op.Counter;
        sender.IncreasePaidStorageCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        if (contract != sender)
        {
            Db.TryAttach(contract);
            contract.IncreasePaidStorageCount++;
            contract.LastLevel = op.Level;
            contract.LastTimestamp = op.Timestamp;
        }

        block.Operations |= XOperations.IncreasePaidStorage;

        Cache.Chain.Get().IncreasePaidStorageOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            BurnFee(sender, op.StorageFee ?? 0);
        }
        #endregion

        Db.IncreasePaidStorageOps.Add(op);
        Context.IncreasePaidStorageOps.Add(op);

        return op;
    }

    public virtual async Task Revert(XIncreasePaidStorageOperation operation)
    {
        #region entities
        var sender = (await Cache.Addresses.GetAsync(operation.SenderId) as XMichelsonUser)!;
        var contract = (await Cache.Addresses.GetAsync(operation.ContractId) as XMichelsonAddress)!;

        Db.TryAttach(sender);
        Db.TryAttach(contract);
        #endregion

        #region revert result
        if (operation.Status == OperationStatus.Applied)
        {
            RevertBurnFee(sender, operation.StorageFee ?? 0);
        }
        #endregion

        #region revert operation
        RevertPayFee(sender, operation.DaFee);
        RevertBurnFee(sender, operation.GasFee - operation.GasRefund);
        sender.Counter = operation.Counter - 1;
        sender.Revealed = true;
        sender.IncreasePaidStorageCount--;
        sender.LastLevel = operation.Level;
        sender.LastTimestamp = operation.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXMichelsonUser(sender);

        if (contract != sender)
        {
            contract.IncreasePaidStorageCount--;
            contract.LastLevel = operation.Level;
            contract.LastTimestamp = operation.Timestamp;
            if (contract.IsEmpty()) await Helpers.RemoveXMichelsonAddress(contract);
        }

        Cache.Chain.Get().IncreasePaidStorageOpsCount--;
        #endregion

        Db.IncreasePaidStorageOps.Remove(operation);
        Cache.Chain.ReleaseOperationId();
    }
}
