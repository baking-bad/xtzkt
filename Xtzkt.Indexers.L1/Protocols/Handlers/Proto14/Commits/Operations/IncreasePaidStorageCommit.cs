using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class IncreasePaidStorageCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var contract = await Cache.Addresses.GetOrCreateAsync(content.RequiredString("destination"), block);

            var result = content.Required("metadata").Required("operation_result");
            var balanceUpdate = result.OptionalArray("balance_updates")?.EnumerateArray()
                .FirstOrDefault(x => x.RequiredString("kind") == "burned" && x.RequiredString("category") == "storage fees");
            var storageFee = balanceUpdate is JsonElement el && el.ValueKind != JsonValueKind.Undefined
                ? el.RequiredInt64("change")
                : 0;

            var operation = new L1IncreasePaidStorageOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredString("hash"),
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                ContractId = contract.Id,
                Amount = BigInteger.Parse(content.RequiredString("amount")),
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
                StorageUsed = (int)(storageFee / Context.Protocol.ByteCost),
                StorageFee = storageFee
            };
            #endregion

            #region entities
            Db.TryAttach(sender);
            Db.TryAttach(contract);
            #endregion

            #region apply operation
            PayFee(sender, operation.BakerFee);

            sender.IncreasePaidStorageCount++;
            if (contract != sender) contract.IncreasePaidStorageCount++;

            block.Operations |= L1Operations.IncreasePaidStorage;

            sender.Counter = operation.Counter;

            Cache.Chain.Get().IncreasePaidStorageOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                var burned = operation.StorageFee ?? 0;
                Proto.Manager.Burn(burned);
                BurnFee(sender, burned);
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.IncreasePaidStorageOps.Add(operation);
            Context.IncreasePaidStorageOps.Add(operation);
        }

        public virtual async Task Revert(L1Block block, L1IncreasePaidStorageOperation operation)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(operation.SenderId);
            var contract = await Cache.Addresses.GetAsync(operation.ContractId);

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
            RevertPayFee(sender, operation.BakerFee);

            sender.IncreasePaidStorageCount--;
            if (contract != sender) contract.IncreasePaidStorageCount--;

            sender.Counter = operation.Counter - 1;
            (sender as L1User)!.Revealed = true;

            Cache.Chain.Get().IncreasePaidStorageOpsCount--;
            #endregion

            Db.IncreasePaidStorageOps.Remove(operation);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }
    }
}
