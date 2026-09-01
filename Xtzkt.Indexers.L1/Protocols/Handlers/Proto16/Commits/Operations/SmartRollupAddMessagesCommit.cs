using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto16
{
    class SmartRollupAddMessagesCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));

            var result = content.Required("metadata").Required("operation_result");

            var operation = new SmartRollupAddMessagesOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                MessagesCount = 0,
                Status = result.RequiredString("status") switch
                {
                    "applied" => OperationStatus.Applied,
                    "backtracked" => OperationStatus.Backtracked,
                    "failed" => OperationStatus.Failed,
                    "skipped" => OperationStatus.Skipped,
                    _ => throw new NotImplementedException()
                },
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
                StorageUsed = 0,
                StorageFee = null,
                AllocationFee = null
            };
            #endregion

            #region entities
            Db.TryAttach(sender);
            #endregion

            #region apply operation
            PayFee(sender, operation.BakerFee);

            sender.SmartRollupAddMessagesCount++;

            block.Operations |= L1Operations.SmartRollupAddMessages;

            sender.Counter = operation.Counter;

            Cache.Chain.Get().SmartRollupAddMessagesOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                foreach (var message in content.RequiredArray("message").EnumerateArray())
                {
                    Proto.Inbox.Push(operation.Id, Hex.Parse(message.RequiredString()));
                    operation.MessagesCount++;
                }
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.SmartRollupAddMessagesOps.Add(operation);
            Context.SmartRollupAddMessagesOps.Add(operation);
        }

        public virtual async Task Revert(L1Block block, SmartRollupAddMessagesOperation operation)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(operation.SenderId);

            Db.TryAttach(sender);
            #endregion

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, operation.BakerFee);

            sender.SmartRollupAddMessagesCount--;

            sender.Counter = operation.Counter - 1;
            (sender as L1User)!.Revealed = true;

            Cache.Chain.Get().SmartRollupAddMessagesOpsCount--;
            #endregion

            Db.SmartRollupAddMessagesOps.Remove(operation);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }
    }
}
