using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class VdfRevelationCommit : ProtocolCommit
    {
        public VdfRevelationCommit(ProtocolHandler protocol) : base(protocol) { }

        public virtual Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var balanceUpdates = content
                .Required("metadata")
                .RequiredArray("balance_updates")
                .EnumerateArray()
                .ToList();

            var (rewardDelegated, rewardStakedOwn, rewardStakedEdge, rewardStakedShared) = ParseRewards(Context.Proposer, balanceUpdates);

            var revelation = new VdfRevelationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),
                BakerId = Context.Proposer.Id,
                Cycle = block.Cycle,
                Solution = Hex.Parse(content.RequiredArray("solution", 2)[0].RequiredString()),
                Proof = Hex.Parse(content.RequiredArray("solution", 2)[1].RequiredString()),
                RewardDelegated = rewardDelegated,
                RewardStakedOwn = rewardStakedOwn,
                RewardStakedEdge = rewardStakedEdge,
                RewardStakedShared = rewardStakedShared
            };
            #endregion

            #region apply operation
            ReceiveRewards(Context.Proposer, revelation.RewardDelegated, revelation.RewardStakedOwn, revelation.RewardStakedEdge, revelation.RewardStakedShared);
            Context.Proposer.VdfRevelationsCount++;

            Cache.Chain.Get().VdfRevelationOpsCount++;

            block.Operations |= L1Operations.VdfRevelation;

            Cache.Statistics.Current.TotalCreated += revelation.RewardDelegated + revelation.RewardStakedOwn + revelation.RewardStakedEdge + revelation.RewardStakedShared;
            Cache.Statistics.Current.TotalFrozen += revelation.RewardStakedOwn + revelation.RewardStakedEdge + revelation.RewardStakedShared;
            #endregion

            Db.VdfRevelationOps.Add(revelation);
            Context.VdfRevelationOps.Add(revelation);
            return Task.CompletedTask;
        }

        public virtual Task Revert(L1Block block, VdfRevelationOperation revelation)
        {
            #region entities
            var blockBaker = Context.Proposer;
            //Db.TryAttach(blockBaker);
            #endregion

            #region apply operation
            RevertReceiveRewards(blockBaker, revelation.RewardDelegated, revelation.RewardStakedOwn, revelation.RewardStakedEdge, revelation.RewardStakedShared);
            blockBaker.VdfRevelationsCount--;

            Cache.Chain.Get().VdfRevelationOpsCount--;
            #endregion

            Db.VdfRevelationOps.Remove(revelation);
            Cache.Chain.ReleaseOperationId();
            return Task.CompletedTask;
        }

        protected virtual (long, long, long, long) ParseRewards(L1Baker proposer, List<JsonElement> balanceUpdates)
        {
            var freezerUpdate = balanceUpdates.SingleOrDefault(x => x.RequiredString("kind") == "freezer");
            var contractUpdate = balanceUpdates.SingleOrDefault(x => x.RequiredString("kind") == "contract");

            var rewardDelegated = contractUpdate.ValueKind != JsonValueKind.Undefined
                ? contractUpdate.RequiredInt64("change")
                : 0;
            var rewardStakedOwn = freezerUpdate.ValueKind != JsonValueKind.Undefined
                ? freezerUpdate.RequiredInt64("change")
                : 0;

            return (rewardDelegated, rewardStakedOwn, 0L, 0L);
        }
    }
}
