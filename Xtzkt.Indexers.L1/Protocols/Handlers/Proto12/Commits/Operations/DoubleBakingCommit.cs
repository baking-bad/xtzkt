using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class DoubleBakingCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual void Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var balanceUpdates = content.Required("metadata").RequiredArray("balance_updates").EnumerateArray();
            var freezerUpdates = balanceUpdates.Where(x => x.RequiredString("kind") == "freezer" && x.RequiredString("category") == "deposits");
            var contractUpdates = balanceUpdates.Where(x => x.RequiredString("kind") == "contract");

            var offenderAddr = freezerUpdates.Any()
                ? freezerUpdates.First().RequiredString("delegate")
                : Context.Proposer.Hash; // this is wrong, but no big deal

            var offenderLoss = freezerUpdates.Any()
                ? -freezerUpdates.Sum(x => x.RequiredInt64("change"))
                : 0;

            var accuserReward = contractUpdates.Any()
                ? contractUpdates.Sum(x => x.RequiredInt64("change"))
                : 0;

            var accuser = Context.Proposer;
            var offender = Cache.Addresses.GetExistingBaker(offenderAddr);

            var doubleBaking = new DoubleBakingOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),

                SlashedLevel = block.Level,
                AccusedLevel = content.Required("bh1").RequiredInt32("level"),
                AccuserId = accuser.Id,
                OffenderId = offender.Id,

                Reward = accuserReward,
                LostStaked = offenderLoss,
                LostUnstaked = 0,
                LostExternalStaked = 0,
                LostExternalUnstaked = 0
            };
            #endregion

            #region entities
            Db.TryAttach(accuser);
            Db.TryAttach(offender);
            #endregion

            #region apply operation
            Receive(accuser, accuser, doubleBaking.Reward);
            
            Spend(offender, offender, doubleBaking.LostStaked);

            accuser.DoubleBakingCount++;
            if (offender != accuser) offender.DoubleBakingCount++;

            block.Operations |= L1Operations.DoubleBaking;

            Cache.Chain.Get().DoubleBakingOpsCount++;
            Cache.Statistics.Current.TotalBurned += doubleBaking.LostStaked - doubleBaking.Reward;
            Cache.Statistics.Current.TotalFrozen -= doubleBaking.LostStaked;
            #endregion

            Db.DoubleBakingOps.Add(doubleBaking);
            Context.DoubleBakingOps.Add(doubleBaking);
        }

        public virtual void Revert(L1Block block, DoubleBakingOperation doubleBaking)
        {
            #region entities
            var accuser = Cache.Addresses.GetBaker(doubleBaking.AccuserId);
            var offender = Cache.Addresses.GetBaker(doubleBaking.OffenderId);
            Db.TryAttach(accuser);
            Db.TryAttach(offender);
            #endregion

            #region apply operation
            RevertReceive(accuser, accuser, doubleBaking.Reward);

            RevertSpend(offender, offender, doubleBaking.LostStaked);

            accuser.DoubleBakingCount--;
            if (offender != accuser) offender.DoubleBakingCount--;

            Cache.Chain.Get().DoubleBakingOpsCount--;
            #endregion

            Db.DoubleBakingOps.Remove(doubleBaking);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
