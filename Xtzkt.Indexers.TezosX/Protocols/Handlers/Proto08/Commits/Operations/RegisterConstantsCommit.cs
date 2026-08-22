using System.Text.Json;
using Netezos.Encoding;
using Netezos.Forging;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08
{
    class RegisterConstantsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task<XRegisterConstantOperation> Apply(string hash, JsonElement content, bool isDelayedOp, bool isFirstOp)
        {
            #region init
            var block = Context.Block;
            var senderAddress = content.RequiredString("source");
            var sender = await GetOrCreateXMichelsonUser(senderAddress);

            var metadata = content.Required("metadata");
            var result = metadata.Required("operation_result");

            var fee = content.RequiredInt64("fee");
            var counter = content.RequiredInt32("counter");
            var gasLimit = content.RequiredInt32("gas_limit");
            var storageLimit = content.RequiredInt32("storage_limit");
            var value = content.RequiredMicheline("value");
            var status = result.RequiredOpStatus("status");

            var daFee = 0L;
            if (!isDelayedOp)
            {
                var size = LocalForge.ForgeRegisterConstant(new()
                {
                    Source = senderAddress,
                    Counter = counter,
                    Fee = fee,
                    GasLimit = gasLimit,
                    StorageLimit = storageLimit,
                    Value = value,
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

            var storageSize = result.OptionalInt32("storage_size");
            var (storageFee, _) = GetStorageFees(result, false, storageSize);

            var op = new XRegisterConstantOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Hash = hash,
                DaFee = daFee,
                GasFee = gasFee,
                GasRefund = gasRefund,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Counter = counter,
                GasLimit = gasLimit,
                StorageLimit = storageLimit,
                SenderId = sender.Id,
                Status = status,
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
                StorageUsed = storageSize ?? 0,
                StorageFee = storageFee,
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, op.DaFee);
            BurnFee(sender, op.GasFee - op.GasRefund);
            sender.Counter = op.Counter;
            sender.RegisterConstantsCount++;
            sender.LastLevel = op.Level;
            sender.LastTimestamp = op.Timestamp;

            block.Operations |= XOperations.RegisterConstant;

            Cache.Chain.Get().RegisterConstantOpsCount++;
            #endregion

            #region apply result
            if (op.Status == OperationStatus.Applied)
            {
                BurnFee(sender, op.StorageFee ?? 0);

                op.Address = result.RequiredString("global_address");
                op.Value = value.ToBytes();
                op.Refs = 0;

                Cache.Chain.Get().ConstantsCount++;
            }
            #endregion

            Db.RegisterConstantOps.Add(op);
            Context.RegisterConstantOps.Add(op);

            return op;
        }

        public virtual async Task Revert(XRegisterConstantOperation op)
        {
            #region entities
            var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XMichelsonUser)!;

            Db.TryAttach(sender);
            #endregion

            #region revert result
            if (op.Status == OperationStatus.Applied)
            {
                RevertBurnFee(sender, op.StorageFee ?? 0);

                Cache.Chain.Get().ConstantsCount--;
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, op.DaFee);
            RevertBurnFee(sender, op.GasFee - op.GasRefund);
            sender.Counter = op.Counter - 1;
            sender.Revealed = true;
            sender.RegisterConstantsCount--;
            sender.LastLevel = op.Level;
            sender.LastTimestamp = op.Timestamp;
            if (sender.IsEmpty()) await RemoveXMichelsonUser(sender);

            Cache.Chain.Get().RegisterConstantOpsCount--;
            #endregion

            Db.RegisterConstantOps.Remove(op);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
