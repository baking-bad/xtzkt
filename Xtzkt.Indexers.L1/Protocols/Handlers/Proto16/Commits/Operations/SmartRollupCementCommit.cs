using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto16
{
    class SmartRollupCementCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var rollup = await Cache.Addresses.GetSmartRollupOrDefaultAsync(content.RequiredString("rollup"));
            var commitment = await Cache.SmartRollupCommitments.GetOrDefaultAsync(GetCommitment(content) is string _c ? Hashes.ParseSrc1Hash(_c) : null, rollup?.Id);

            var result = content.Required("metadata").Required("operation_result");

            var operation = new SmartRollupCementOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                SmartRollupId = rollup?.Id,
                CommitmentId = commitment?.Id,
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
            Db.TryAttach(commitment);
            #endregion

            #region apply operation
            PayFee(sender, operation.BakerFee);

            sender.SmartRollupCementCount++;
            if (rollup != null) rollup.SmartRollupCementCount++;

            block.GasUsed += operation.GasUsed;
            block.Operations |= L1Operations.SmartRollupCement;

            sender.Counter = operation.Counter;

            commitment?.LastLevel = operation.Level;

            Cache.Chain.Get().SmartRollupCementOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                rollup!.InboxLevel = commitment!.InboxLevel;
                rollup.LastCommitment = Hashes.FormatSrc1Hash(commitment.Hash);
                rollup.CementedCommitments++;
                rollup.PendingCommitments--;
                
                commitment.Status = SmartRollupCommitmentStatus.Cemented;
            }
            #endregion

            Proto.Manager.Set(sender);
            Proto.Manager.Add(operation);
            Db.SmartRollupCementOps.Add(operation);
            Context.SmartRollupCementOps.Add(operation);
        }

        public virtual async Task Revert(L1Block block, SmartRollupCementOperation operation)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(operation.SenderId);
            var rollup = await Cache.Addresses.GetAsync(operation.SmartRollupId) as L1SmartRollup;
            var commitment = await Cache.SmartRollupCommitments.GetOrDefaultAsync(operation.CommitmentId);

            Db.TryAttach(sender);
            Db.TryAttach(rollup);
            Db.TryAttach(commitment);
            #endregion

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
                var prevCement = await Db.SmartRollupCementOps.AsNoTracking()
                    .Where(x => x.SmartRollupId == operation.SmartRollupId && x.Id < operation.Id && x.Status == OperationStatus.Applied)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();
                var prevCementedCommitment = await Cache.SmartRollupCommitments.GetOrDefaultAsync(prevCement?.CommitmentId);

                rollup!.InboxLevel = prevCementedCommitment?.InboxLevel ?? 0;
                rollup.LastCommitment = prevCementedCommitment?.Hash is byte[] _pc ? Hashes.FormatSrc1Hash(_pc) : rollup.GenesisCommitment;
                rollup.CementedCommitments--;
                rollup.PendingCommitments++;

                commitment!.Status = SmartRollupCommitmentStatus.Pending;
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, operation.BakerFee);

            sender.SmartRollupCementCount--;
            if (rollup != null) rollup.SmartRollupCementCount--;

            sender.Counter = operation.Counter - 1;
            (sender as L1User)!.Revealed = true;

            // commitment.LastLevel is not reverted

            Cache.Chain.Get().SmartRollupCementOpsCount--;
            #endregion

            Db.SmartRollupCementOps.Remove(operation);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual string? GetCommitment(JsonElement content) => content.RequiredString("commitment");
    }
}
