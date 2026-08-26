using Netezos.Forging;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08
{
    class RevealsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task<XRevealOperation> Apply(string hash, JsonElement content, bool isDelayedOp, bool isFirstOp)
        {
            #region init
            var block = Context.Block;
            var senderAddress = content.RequiredString("source");
            var sender = await Helpers.GetOrCreateXMichelsonUser(senderAddress);

            var metadata = content.Required("metadata");
            var result = metadata.Required("operation_result");

            var fee = content.RequiredInt64("fee");
            var counter = content.RequiredInt32("counter");
            var gasLimit = content.RequiredInt32("gas_limit");
            var storageLimit = content.RequiredInt32("storage_limit");
            var pubKey = content.RequiredString("public_key");
            var status = result.RequiredOpStatus("status");

            var daFee = 0L;
            if (!isDelayedOp)
            {
                var size = LocalForge.ForgeReveal(new()
                {
                    Source = senderAddress,
                    Counter = counter,
                    GasLimit = gasLimit,
                    StorageLimit = storageLimit,
                    Fee = fee,
                    PublicKey = pubKey,
                    Proof = content.OptionalString("proof")
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

            var op = new XRevealOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Hash = hash,
                Level = block.Level,
                Timestamp = block.Timestamp,
                DaFee = daFee,
                GasFee = gasFee,
                GasRefund = gasRefund,
                Counter = counter,
                GasLimit = gasLimit,
                StorageLimit = storageLimit,
                SenderId = sender.Id,
                Status = status,
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000)
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, op.DaFee);
            BurnFee(sender, op.GasFee - op.GasRefund);
            sender.Counter = op.Counter;
            sender.RevealsCount++;
            sender.LastLevel = op.Level;
            sender.LastTimestamp = op.Timestamp;

            block.Operations |= XOperations.Reveal;

            Cache.Chain.Get().RevealOpsCount++;
            #endregion

            #region apply result
            if (op.Status == OperationStatus.Applied)
            {
                sender.PublicKey = pubKey;
                sender.Revealed = true;
            }
            #endregion

            Db.RevealOps.Add(op);
            Context.RevealOps.Add(op);

            return op;
        }

        public virtual async Task Revert(XRevealOperation operation)
        {
            #region entities
            var sender = (await Cache.Addresses.GetAsync(operation.SenderId) as XMichelsonUser)!;

            Db.TryAttach(sender);
            #endregion

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
                if (sender.RevealsCount == 1)
                {
                    sender.PublicKey = null;
                    sender.Revealed = false;
                }
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, operation.DaFee);
            RevertBurnFee(sender, operation.GasFee - operation.GasRefund);
            sender.Counter = operation.Counter - 1;
            sender.RevealsCount--;
            sender.LastLevel = operation.Level;
            sender.LastTimestamp = operation.Timestamp;
            if (sender.IsEmpty()) await Helpers.RemoveXMichelsonUser(sender);

            Cache.Chain.Get().RevealOpsCount--;
            #endregion

            Db.RevealOps.Remove(operation);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
