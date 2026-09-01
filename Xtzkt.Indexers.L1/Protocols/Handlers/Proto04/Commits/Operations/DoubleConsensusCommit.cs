using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto04
{
    class DoubleConsensusCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var balanceUpdates = content.Required("metadata").RequiredArray("balance_updates").EnumerateArray();

            var offenderAddr = balanceUpdates
                .First(x => x.RequiredInt64("change") < 0).RequiredString("delegate");

            var rewards = balanceUpdates
                .FirstOrDefault(x => x.RequiredString("category")[0] == 'r' && x.RequiredInt64("change") > 0);

            var lostDeposits = balanceUpdates
                .FirstOrDefault(x => x.RequiredString("category")[0] == 'd' && x.RequiredInt64("change") < 0);
            var lostDepositsValue = lostDeposits.ValueKind != JsonValueKind.Undefined ? -lostDeposits.RequiredInt64("change") : 0;

            var lostRewards = balanceUpdates
                .FirstOrDefault(x => x.RequiredString("category")[0] == 'r' && x.RequiredInt64("change") < 0);
            var lostRewardsValue = lostRewards.ValueKind != JsonValueKind.Undefined ? -lostRewards.RequiredInt64("change") : 0;

            var lostFees = balanceUpdates
                .FirstOrDefault(x => x.RequiredString("category")[0] == 'f' && x.RequiredInt64("change") < 0);
            var lostFeesValue = lostFees.ValueKind != JsonValueKind.Undefined ? -lostFees.RequiredInt64("change") : 0;

            var accuser = Context.Proposer;
            var offender = Cache.Addresses.GetExistingBaker(offenderAddr);

            var doubleConsensus = new DoubleConsensusOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),

                Kind = DoubleConsensusKind.DoubleAttestation,

                SlashedLevel = block.Level,
                AccusedLevel = content.Required("op1").Required("operations").RequiredInt32("level"),

                AccuserId = accuser.Id,
                OffenderId = offender.Id,

                Reward = rewards.ValueKind != JsonValueKind.Undefined ? rewards.RequiredInt64("change") : 0,
                LostStaked = lostDepositsValue + lostRewardsValue + lostFeesValue,
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
            ReceiveLockedRewards(accuser, doubleConsensus.Reward);
            Spend(offender, offender, lostDepositsValue + lostFeesValue);
            BurnLockedRewards(offender, lostRewardsValue);

            accuser.DoubleConsensusCount++;
            if (offender != accuser) offender.DoubleConsensusCount++;

            block.Operations |= L1Operations.DoubleConsensus;

            Cache.Chain.Get().DoubleConsensusOpsCount++;
            Cache.Statistics.Current.TotalBurned += doubleConsensus.LostStaked - doubleConsensus.Reward;
            Cache.Statistics.Current.TotalFrozen -= doubleConsensus.LostStaked - doubleConsensus.Reward;
            #endregion

            Db.DoubleConsensusOps.Add(doubleConsensus);
            Context.DoubleConsensusOps.Add(doubleConsensus);
            return Task.CompletedTask;
        }

        public virtual Task Revert(L1Block block, DoubleConsensusOperation doubleConsensus)
        {
            #region entities
            //var block = doubleConsensus.Block;
            var accuser = Cache.Addresses.GetBaker(doubleConsensus.AccuserId);
            var offender = Cache.Addresses.GetBaker(doubleConsensus.OffenderId);

            //Db.TryAttach(block);
            Db.TryAttach(accuser);
            Db.TryAttach(offender);
            #endregion

            #region apply operation
            RevertReceiveLockedRewards(accuser, doubleConsensus.Reward);
            // here we can miss 1 mutez, but this may happen only in legacy protocols
            // TODO: replace it with NotImplementedException after Ithaca
            RevertSpend(offender, offender, doubleConsensus.Reward * 2);
            RevertBurnLockedRewards(offender, doubleConsensus.LostStaked - doubleConsensus.Reward * 2);

            accuser.DoubleConsensusCount--;
            if (offender != accuser) offender.DoubleConsensusCount--;

            Cache.Chain.Get().DoubleConsensusOpsCount--;
            #endregion

            Db.DoubleConsensusOps.Remove(doubleConsensus);
            Cache.Chain.ReleaseOperationId();
            return Task.CompletedTask;
        }
    }
}
