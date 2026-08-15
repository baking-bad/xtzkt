using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class BlockCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public L1Block Block { get; private set; } = null!;

        public virtual async Task Apply(JsonElement rawBlock)
        {
            var level = rawBlock.Required("header").RequiredInt32("level");
            var protocol = await Cache.Protocols.GetAsync(rawBlock.RequiredString("protocol"));
            var events = L1BlockEvents.None;

            var metadata = rawBlock.Required("metadata");
            var (deposit, reward) = ParseBalanceUpdates(metadata.RequiredArray("balance_updates"));

            if (protocol.IsCycleStart(level))
                events |= L1BlockEvents.CycleBegin;
            else if (protocol.IsCycleEnd(level))
                events |= L1BlockEvents.CycleEnd;

            if (protocol.FirstLevel == level)
                events |= L1BlockEvents.ProtocolBegin;
            else if (metadata.RequiredString("protocol") != metadata.RequiredString("next_protocol"))
                events |= L1BlockEvents.ProtocolEnd;

            if (metadata.RequiredArray("deactivated").Count() > 0)
                events |= L1BlockEvents.Deactivations;

            if (level % protocol.BlocksPerSnapshot == 0)
                events |= L1BlockEvents.BalanceSnapshot;

            var round = rawBlock.Required("header").RequiredInt32("priority");
            var baker = Cache.Addresses.GetExistingBaker(rawBlock.Required("metadata").RequiredString("baker"));

            var chain = Cache.Chain.Get();
            var timestamp = rawBlock.Required("header").RequiredDateTime("timestamp");
            Block = new L1Block
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = chain.Id,
                Hash = rawBlock.RequiredString("hash"),
                Cycle = protocol.GetCycle(level),
                Level = level,
                ProtocolId = protocol.Id,
                Timestamp = timestamp,
                AttestationCommittee = protocol.AttestersPerBlock,
                PayloadRound = round,
                BlockRound = round,
                ProposerId = baker.Id,
                ProducerId = baker.Id,
                Events = events,
                RewardDelegated = reward,
                LBToggle = GetLBToggleVote(rawBlock),
                LBToggleEma = GetLBToggleEma(rawBlock)
            };

            Context.Block = Block;
            Context.Proposer = baker;
            Context.Protocol = protocol;

            #region entities
            Db.TryAttach(protocol);
            Db.TryAttach(baker);
            #endregion

            ReceiveLockedRewards(baker, Block.RewardDelegated);
            baker.BlocksCount++;

            var newDeactivationLevel = baker.Staked ? GracePeriod.Reset(Block.Level, protocol) : GracePeriod.Init(Block.Level, protocol);
            if (baker.DeactivationLevel < newDeactivationLevel)
            {
                if (baker.DeactivationLevel <= Block.Level)
                    await ActivateBaker(baker);

                Block.ResetBakerDeactivation = baker.DeactivationLevel;
                baker.DeactivationLevel = newDeactivationLevel;
            }

            if (Block.Events.HasFlag(L1BlockEvents.ProtocolEnd))
                protocol.LastLevel = Block.Level;

            Cache.Chain.Get().BlocksCount++;
            Cache.Statistics.Current.TotalCreated += Block.RewardDelegated;
            Cache.Statistics.Current.TotalFrozen += Block.RewardDelegated + deposit + Block.BakerFees;

            Db.Blocks.Add(Block);
            Cache.Blocks.Add(Block);
        }

        public virtual async Task Revert(L1Block block)
        {
            Block = block;

            #region entities
            var baker = Context.Proposer;
            Db.TryAttach(baker);
            #endregion

            RevertReceiveLockedRewards(baker, Block.RewardDelegated);
            baker.BlocksCount--;

            if (Block.ResetBakerDeactivation != null)
            {
                if (Block.ResetBakerDeactivation <= Block.Level)
                    await DeactivateBaker(baker);

                baker.DeactivationLevel = (int)Block.ResetBakerDeactivation;
            }

            Cache.Chain.Get().BlocksCount--;

            Db.Blocks.Remove(Block);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual (long, long) ParseBalanceUpdates(JsonElement balanceUpdates)
        {
            var deposit = 0L;
            var reward = 0L;
            foreach (var bu in balanceUpdates.EnumerateArray().Take(3))
            {
                if (bu.RequiredString("kind")[0] == 'f')
                {
                    var change = bu.RequiredInt64("change");
                    if (change > 0)
                    {
                        if (bu.RequiredString("category")[0] == 'd')
                            deposit = change;
                        else
                            reward = change;
                    }

                }
            }
            return (deposit, reward);
        }

        protected virtual bool? GetLBToggleVote(JsonElement block) => null;

        protected virtual int GetLBToggleEma(JsonElement block) => 0;
    }
}
