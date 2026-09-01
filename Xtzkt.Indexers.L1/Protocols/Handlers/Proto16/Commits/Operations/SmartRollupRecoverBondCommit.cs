using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto16
{
    class SmartRollupRecoverBondCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var rollup = await Cache.Addresses.GetSmartRollupOrDefaultAsync(content.RequiredString("rollup"));
            var staker = await Cache.Addresses.GetAsync(content.RequiredString("staker"), block);

            var result = content.Required("metadata").Required("operation_result");
            var bond = result.OptionalArray("balance_updates")?.EnumerateArray()
                .FirstOrDefault(x => x.RequiredString("kind") == "contract") ?? default;

            var operation = new SmartRollupRecoverBondOperation
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
                SmartRollupId = rollup?.Id,
                StakerId = staker?.Id,
                Bond = bond.ValueKind == JsonValueKind.Undefined ? 0 : bond.RequiredInt64("change"),
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
            Db.TryAttach(rollup);
            Db.TryAttach(staker);
            #endregion

            #region apply operation
            PayFee(sender, operation.BakerFee);

            sender.SmartRollupRecoverBondCount++;
            if (rollup != null) rollup.SmartRollupRecoverBondCount++;
            if (staker != null && staker.Id != sender.Id) staker.SmartRollupRecoverBondCount++;

            block.Operations |= L1Operations.SmartRollupRecoverBond;

            sender.Counter = operation.Counter;

            Cache.Chain.Get().SmartRollupRecoverBondOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                staker!.SmartRollupBonds -= operation.Bond;
                rollup!.SmartRollupBonds -= operation.Bond;
                rollup.ActiveStakers--;

                var bondOp = Context.SmartRollupPublishOps
                    .FirstOrDefault(x => x.SmartRollupId == operation.SmartRollupId && x.BondStatus == SmartRollupBondStatus.Active && x.SenderId == operation.StakerId)
                    ?? await Db.SmartRollupPublishOps.FirstAsync(x => x.SmartRollupId == operation.SmartRollupId && x.BondStatus == SmartRollupBondStatus.Active && x.SenderId == operation.StakerId);
                bondOp.BondStatus = SmartRollupBondStatus.Returned;

                Cache.Statistics.Current.TotalSmartRollupBonds -= operation.Bond;
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.SmartRollupRecoverBondOps.Add(operation);
            Context.SmartRollupRecoverBondOps.Add(operation);
        }

        public virtual async Task Revert(L1Block block, SmartRollupRecoverBondOperation operation)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(operation.SenderId);
            var rollup = await Cache.Addresses.GetAsync(operation.SmartRollupId) as L1SmartRollup;
            var staker = await Cache.Addresses.GetAsync(operation.StakerId);

            Db.TryAttach(sender);
            Db.TryAttach(rollup);
            Db.TryAttach(staker);
            #endregion

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
                staker!.SmartRollupBonds += operation.Bond;
                rollup!.SmartRollupBonds += operation.Bond;
                rollup.ActiveStakers++;

                var bondOp = await Db.SmartRollupPublishOps
                    .OrderByDescending(x => x.Id)
                    .FirstAsync(x =>
                        x.SmartRollupId == operation.SmartRollupId &&
                        x.BondStatus == SmartRollupBondStatus.Returned &&
                        x.SenderId == operation.StakerId);

                bondOp.BondStatus = SmartRollupBondStatus.Active;
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, operation.BakerFee);

            sender.SmartRollupRecoverBondCount--;
            if (rollup != null) rollup.SmartRollupRecoverBondCount--;
            if (staker != null && staker.Id != sender.Id) staker.SmartRollupRecoverBondCount--;

            sender.Counter = operation.Counter - 1;
            (sender as L1User)!.Revealed = true;

            Cache.Chain.Get().SmartRollupRecoverBondOpsCount--;
            #endregion

            Db.SmartRollupRecoverBondOps.Remove(operation);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }
    }
}
