using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class VdfRevelationCommit : ProtocolCommit
    {
        public VdfRevelationCommit(ProtocolHandler protocol) : base(protocol) { }

        public virtual Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var balanceUpdate = content.Required("metadata").RequiredArray("balance_updates").EnumerateArray()
                   .FirstOrDefault(x => x.RequiredString("kind") == "contract");
            var reward = balanceUpdate.ValueKind != JsonValueKind.Undefined
                ? balanceUpdate.RequiredInt64("change")
                : 0;

            var revelation = new VdfRevelationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredString("hash"),
                BakerId = Context.Proposer.Id,
                Cycle = block.Cycle,
                RewardDelegated = reward,
                Solution = Hex.Parse(content.RequiredArray("solution", 2)[0].RequiredString()),
                Proof = Hex.Parse(content.RequiredArray("solution", 2)[1].RequiredString())
            };
            #endregion

            #region entities
            var blockBaker = Context.Proposer;
            //Db.TryAttach(blockBaker);
            #endregion

            #region apply operation
            Receive(blockBaker, blockBaker, revelation.RewardDelegated);

            blockBaker.VdfRevelationsCount++;
            Cache.Chain.Get().VdfRevelationOpsCount++;

            block.Operations |= L1Operations.VdfRevelation;

            Cache.Statistics.Current.TotalCreated += revelation.RewardDelegated;
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
            RevertReceive(blockBaker, blockBaker, revelation.RewardDelegated);

            blockBaker.VdfRevelationsCount--;
            Cache.Chain.Get().VdfRevelationOpsCount--;
            #endregion

            Db.VdfRevelationOps.Remove(revelation);
            Cache.Chain.ReleaseOperationId();
            return Task.CompletedTask;
        }
    }
}
