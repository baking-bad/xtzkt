using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto15
{
    class DrainDelegateCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var baker = Cache.Addresses.GetExistingBaker(content.RequiredString("delegate"));
            var target = (await Cache.Addresses.GetAsync(content.RequiredString("destination"), block))!;

            var balanceUpdates = content.Required("metadata").RequiredArray("balance_updates").EnumerateArray();

            var allocationFeeUpdate = balanceUpdates.SingleOrDefault(x => x.RequiredString("kind") == "burned");
            var allocationFee = allocationFeeUpdate.ValueKind != JsonValueKind.Undefined
                ? allocationFeeUpdate.RequiredInt64("change")
                : 0;

            var deposits = balanceUpdates
                .Where(x => x.RequiredString("kind") == "contract" && x.RequiredInt64("change") > 0)
                .OrderByDescending(x => x.RequiredInt64("change"))
                .ToList();

            var amount = 0L;
            var fee = 0L;

            if (deposits.Count == 2)
            {
                amount = deposits.First(x => x.RequiredString("contract") == target.Hash).RequiredInt64("change");
                fee = deposits.Last(x => x.RequiredString("contract") == Context.Proposer.Hash).RequiredInt64("change");
            }
            else if (deposits.Count == 1)
            {
                if (deposits[0].RequiredString("contract") == target.Hash)
                    amount = deposits[0].RequiredInt64("change");
                else if (deposits[0].RequiredString("contract") == Context.Proposer.Hash)
                    fee = deposits[0].RequiredInt64("change");
                else
                    throw new Exception("Unexpected balance updates behavior");
            }
            else if (deposits.Count != 0)
            {
                throw new Exception("Unexpected balance updates behavior");
            }

            var operation = new DrainDelegateOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),
                Level = block.Level,
                Timestamp = block.Timestamp,
                BakerId = baker.Id,
                TargetId = target.Id,
                Amount = amount,
                Fee = fee,
                AllocationFee = allocationFee
            };
            #endregion

            #region entities
            Db.TryAttach(baker);
            Db.TryAttach(target);
            #endregion

            #region apply operation
            PayFee(baker, operation.Fee);
            BurnFee(baker, operation.AllocationFee);
            Spend(baker, baker, operation.Amount);
            Receive(target, operation.Amount);

            baker.DrainDelegateCount++;
            if (target != baker) target.DrainDelegateCount++;

            block.Operations |= L1Operations.DrainDelegate;

            Cache.Chain.Get().DrainDelegateOpsCount++;
            #endregion

            Db.DrainDelegateOps.Add(operation);
            Context.DrainDelegateOps.Add(operation);
        }

        public virtual async Task Revert(L1Block block, DrainDelegateOperation operation)
        {
            #region entities
            var baker = Cache.Addresses.GetBaker(operation.BakerId);
            Db.TryAttach(baker);
            
            var target = await Cache.Addresses.GetAsync(operation.TargetId);
            Db.TryAttach(target);
            #endregion

            #region apply operation
            RevertPayFee(baker, operation.Fee);
            RevertBurnFee(baker, operation.AllocationFee);
            RevertSpend(baker, baker, operation.Amount);

            RevertReceive(target, operation.Amount);

            baker.DrainDelegateCount--;
            if (target != baker) target.DrainDelegateCount--;

            Cache.Chain.Get().DrainDelegateOpsCount--;
            #endregion

            Db.DrainDelegateOps.Remove(operation);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
