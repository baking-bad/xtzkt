using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class NonceRevelationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var balanceUpdate = content.Required("metadata").RequiredArray("balance_updates").EnumerateArray()
                   .FirstOrDefault(x => x.RequiredString("kind") == "contract");

            var reward = balanceUpdate.ValueKind != JsonValueKind.Undefined
                ? balanceUpdate.RequiredInt64("change")
                : 0;

            var revealedBlock = await Cache.Blocks.GetAsync(content.RequiredInt32("level"));
            var sender = Cache.Addresses.GetBaker(revealedBlock.ProposerId!.Value);

            var revelation = new NonceRevelationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),
                BakerId = Context.Proposer.Id,
                SenderId = sender.Id,
                RevealedLevel = revealedBlock.Level,
                RevealedCycle = revealedBlock.Cycle,
                Nonce = Hex.Parse(content.RequiredString("nonce")),
                RewardDelegated = reward
            };
            #endregion

            #region entities
            var blockBaker = Context.Proposer;

            //Db.TryAttach(blockBaker);
            Db.TryAttach(sender);
            Db.TryAttach(revealedBlock);
            #endregion

            #region apply operation
            Receive(blockBaker, blockBaker, revelation.RewardDelegated);

            sender.NonceRevelationsCount++;
            if (blockBaker != sender) blockBaker.NonceRevelationsCount++;

            block.Operations |= L1Operations.NonceRevelation;

            revealedBlock.RevelationId = revelation.Id;

            Cache.Chain.Get().NonceRevelationOpsCount++;
            Cache.Statistics.Current.TotalCreated += revelation.RewardDelegated;
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
            RevertReceive(blockBaker, blockBaker, revelation.RewardDelegated);

            sender.NonceRevelationsCount--;
            if (blockBaker != sender) blockBaker.NonceRevelationsCount--;

            revealedBlock.RevelationId = null;

            Cache.Chain.Get().NonceRevelationOpsCount--;
            #endregion

            Db.NonceRevelationOps.Remove(revelation);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
