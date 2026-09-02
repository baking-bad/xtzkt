using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class NonceRevelationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var baker = Context.Proposer;

            var balanceUpdates = content
                .Required("metadata")
                .RequiredArray("balance_updates")
                .EnumerateArray()
                .ToList();

            var (rewardDelegated, rewardStakedOwn, rewardStakedEdge, rewardStakedShared) = ParseRewards(Context.Proposer, balanceUpdates);

            var revealedBlock = await Cache.Blocks.GetAsync(content.RequiredInt32("level"));
            var sender = Cache.Addresses.GetBaker(revealedBlock.ProposerId!.Value);

            var revelation = new NonceRevelationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                BakerId = baker.Id,
                SenderId = sender.Id,
                RevealedLevel = revealedBlock.Level,
                RevealedCycle = revealedBlock.Cycle,
                Nonce = Hex.Parse(content.RequiredString("nonce")),
                RewardDelegated = rewardDelegated,
                RewardStakedOwn = rewardStakedOwn,
                RewardStakedEdge = rewardStakedEdge,
                RewardStakedShared = rewardStakedShared
            };
            #endregion

            #region apply operation
            ReceiveRewards(baker, revelation.RewardDelegated, revelation.RewardStakedOwn, revelation.RewardStakedEdge, revelation.RewardStakedShared);
            baker.NonceRevelationsCount++;

            if (revelation.SenderId != baker.Id)
            {
                Db.TryAttach(sender);
                sender.NonceRevelationsCount++;
            }

            Db.TryAttach(revealedBlock);
            revealedBlock.RevelationId = revelation.Id;

            block.Operations |= L1Operations.NonceRevelation;

            Cache.Chain.Get().NonceRevelationOpsCount++;
            Cache.Statistics.Current.TotalCreated += revelation.RewardDelegated + revelation.RewardStakedOwn + revelation.RewardStakedEdge + revelation.RewardStakedShared;
            Cache.Statistics.Current.TotalFrozen += revelation.RewardStakedOwn + revelation.RewardStakedEdge + revelation.RewardStakedShared;
            #endregion

            Db.NonceRevelationOps.Add(revelation);
            Context.NonceRevelationOps.Add(revelation);
        }

        public virtual async Task Revert(L1Block block, NonceRevelationOperation revelation)
        {
            #region entities
            var blockBaker = Context.Proposer;
            var sender = Cache.Addresses.GetBaker(revelation.SenderId);
            var revealedBlock = await Cache.Blocks.GetAsync(revelation.RevealedLevel);

            //Db.TryAttach(blockBaker);
            Db.TryAttach(sender);
            Db.TryAttach(revealedBlock);
            #endregion

            #region apply operation
            RevertReceiveRewards(blockBaker, revelation.RewardDelegated, revelation.RewardStakedOwn, revelation.RewardStakedEdge, revelation.RewardStakedShared);
            blockBaker.NonceRevelationsCount--;

            if (sender.Id != blockBaker.Id)
            {
                Db.TryAttach(sender);
                sender.NonceRevelationsCount--;
            }

            Db.TryAttach(revealedBlock);
            revealedBlock.RevelationId = null;

            Cache.Chain.Get().NonceRevelationOpsCount--;
            #endregion

            Db.NonceRevelationOps.Remove(revelation);
            Cache.Chain.ReleaseOperationId();
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
