using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class SetDepositsLimitCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var sender = (L1User)await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));

            var result = content.Required("metadata").Required("operation_result");
            var limit = content.OptionalString("limit");

            var operation = new SetDepositsLimitOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Hash = op.RequiredString("hash"),
                Level = block.Level,
                Timestamp = block.Timestamp,
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
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
                Limit = limit == null ? null : BigInteger.Parse(limit)
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, operation.BakerFee);
            sender.Counter = operation.Counter;
            sender.SetDepositsLimitsCount++;

            block.Operations |= L1Operations.SetDepositsLimits;

            Cache.Chain.Get().SetDepositsLimitOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                if (operation.Limit != null)
                {
                    (sender as L1Baker)!.FrozenDepositLimit = operation.Limit > long.MaxValue / 100
                        ? long.MaxValue / 100
                        : (long)operation.Limit;
                }
                else
                {
                    (sender as L1Baker)!.FrozenDepositLimit = null;
                }
                UpdateBakerPower((sender as L1Baker)!);
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.SetDepositsLimitOps.Add(operation);
            Context.SetDepositsLimitOps.Add(operation);
        }

        public virtual async Task Revert(L1Block block, SetDepositsLimitOperation op)
        {
            #region entities
            var sender = (L1User)await Cache.Addresses.GetAsync(op.SenderId);

            Db.TryAttach(sender);
            #endregion

            #region revert result
            if (op.Status == OperationStatus.Applied)
            {
                var prevOp = await Db.SetDepositsLimitOps
                    .AsNoTracking()
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync(x => x.SenderId == op.SenderId && x.Status == OperationStatus.Applied && x.Id < op.Id);
                
                if (prevOp?.Limit != null)
                {
                    (sender as L1Baker)!.FrozenDepositLimit = prevOp.Limit > long.MaxValue / 100
                        ? long.MaxValue / 100
                        : (long)prevOp.Limit;
                }
                else
                {
                    (sender as L1Baker)!.FrozenDepositLimit = null;
                }
                RevertBakerPower((sender as L1Baker)!);
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, op.BakerFee);
            sender.SetDepositsLimitsCount--;
            sender.Counter = op.Counter - 1;
            sender.Revealed = true;

            Cache.Chain.Get().SetDepositsLimitOpsCount--;
            #endregion

            Db.SetDepositsLimitOps.Remove(op);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }
    }
}
