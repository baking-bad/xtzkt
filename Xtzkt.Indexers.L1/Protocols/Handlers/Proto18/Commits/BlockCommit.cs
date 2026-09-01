using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class BlockCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public L1Block Block { get; private set; } = null!;

        public virtual async Task Apply(JsonElement rawBlock)
        {
            var header = rawBlock.Required("header");
            var metadata = rawBlock.Required("metadata");

            var level = header.RequiredInt32("level");
            var proposer = Cache.Addresses.GetExistingBaker(metadata.RequiredString("proposer"));
            var producer = Cache.Addresses.GetExistingBaker(metadata.RequiredString("baker"));
            var protocol = await Cache.Protocols.GetAsync(rawBlock.RequiredString("protocol"));
            var events = L1BlockEvents.None;

            if (protocol.IsCycleStart(level))
                events |= L1BlockEvents.CycleBegin;
            else if (protocol.IsCycleEnd(level))
                events |= L1BlockEvents.CycleEnd;

            if (protocol.FirstLevel == level)
                events |= L1BlockEvents.ProtocolBegin;
            else if (protocol.Hash != metadata.RequiredString("next_protocol"))
                events |= L1BlockEvents.ProtocolEnd;

            if (metadata.RequiredArray("deactivated").Count() > 0)
                events |= L1BlockEvents.Deactivations;

            if ((level - Cache.Protocols.GetCycleStart(protocol.GetCycle(level)) + 1) % protocol.BlocksPerSnapshot == 0)
                events |= L1BlockEvents.BalanceSnapshot;

            var payloadRound = header.RequiredInt32("payload_round");
            var blockRound = Hex.Parse(header.RequiredArray("fitness", 5)[4].RequiredString()).ToInt32();
            var lbVote = header.RequiredString("liquidity_baking_toggle_vote");

            var chain = Cache.Chain.Get();
            var timestamp = header.RequiredDateTime("timestamp");
            Block = new L1Block
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = chain.Id,
                Hash = rawBlock.RequiredMichelsonBlockHashBytes("hash"),
                Cycle = protocol.GetCycle(level),
                Level = level,
                ProtocolId = protocol.Id,
                Timestamp = timestamp,
                AttestationCommittee = GetAttestationCommittee(protocol, metadata),
                PayloadRound = payloadRound,
                BlockRound = blockRound,
                ProposerId = proposer.Id,
                ProducerId = producer.Id,
                Events = events,
                LBToggle = lbVote == "on" ? true : lbVote == "off" ? false : null,
                LBToggleEma = metadata.RequiredInt32("liquidity_baking_toggle_ema")
            };

            Context.Block = Block;
            Context.Proposer = proposer;
            Context.Protocol = protocol;

            Db.TryAttach(protocol); // if we don't attach it, ef will recognize it as 'added'
            if (Block.Events.HasFlag(L1BlockEvents.ProtocolEnd))
                protocol.LastLevel = Block.Level;

            Db.TryAttach(proposer); // if we don't attach it, ef will recognize it as 'added'
            Db.TryAttach(producer); // if we don't attach it, ef will recognize it as 'added'
            
            Cache.Chain.Get().BlocksCount++;

            Db.Blocks.Add(Block);
            Cache.Blocks.Add(Block);
        }
        
        public async Task ApplyRewards(JsonElement rawBlock)
        {
            var proposer = Cache.Addresses.GetBaker(Block.ProposerId!.Value);
            var producer = Cache.Addresses.GetBaker(Block.ProducerId!.Value);

            var balanceUpdates = rawBlock
                .Required("metadata")
                .RequiredArray("balance_updates")
                .EnumerateArray()
                .Where(x => x.RequiredString("origin") == "block")
                .ToList();

            var (
                rewardDelegated,
                rewardStakedOwn,
                rewardStakedEdge,
                rewardStakedShared,
                bonusDelegated,
                bonusStakedOwn,
                bonusStakedEdge,
                bonusStakedShared
            ) = ParseRewards(proposer, producer, balanceUpdates);

            Block.RewardDelegated = rewardDelegated;
            Block.RewardStakedOwn = rewardStakedOwn;
            Block.RewardStakedEdge = rewardStakedEdge;
            Block.RewardStakedShared = rewardStakedShared;
            Block.BonusDelegated = bonusDelegated;
            Block.BonusStakedOwn = bonusStakedOwn;
            Block.BonusStakedEdge = bonusStakedEdge;
            Block.BonusStakedShared = bonusStakedShared;

            Db.TryAttach(proposer);
            ReceiveRewards(proposer, Block.RewardDelegated, Block.RewardStakedOwn, Block.RewardStakedEdge, Block.RewardStakedShared);
            proposer.BlocksCount++;

            #region set baker active
            var newDeactivationLevel = proposer.Staked ? GracePeriod.Reset(Block.Level, Context.Protocol) : GracePeriod.Init(Block.Level, Context.Protocol);
            if (proposer.DeactivationLevel < newDeactivationLevel)
            {
                if (proposer.DeactivationLevel <= Block.Level)
                    await ActivateBaker(proposer);

                Block.ResetBakerDeactivation = proposer.DeactivationLevel;
                proposer.DeactivationLevel = newDeactivationLevel;
            }
            #endregion

            Db.TryAttach(producer);
            ReceiveRewards(producer, Block.BonusDelegated, Block.BonusStakedOwn, Block.BonusStakedEdge, Block.BonusStakedShared);
            if (producer != proposer)
            {
                producer.BlocksCount++;

                #region set proposer active
                newDeactivationLevel = producer.Staked ? GracePeriod.Reset(Block.Level, Context.Protocol) : GracePeriod.Init(Block.Level, Context.Protocol);
                if (producer.DeactivationLevel < newDeactivationLevel)
                {
                    if (producer.DeactivationLevel <= Block.Level)
                        await ActivateBaker(producer);

                    Block.ResetProposerDeactivation = producer.DeactivationLevel;
                    producer.DeactivationLevel = newDeactivationLevel;
                }
                #endregion
            }

            Cache.Statistics.Current.TotalCreated +=
                Block.RewardDelegated + Block.RewardStakedOwn + Block.RewardStakedEdge + Block.RewardStakedShared +
                Block.BonusDelegated + Block.BonusStakedOwn + Block.BonusStakedEdge + Block.BonusStakedShared;

            Cache.Statistics.Current.TotalFrozen +=
                Block.RewardStakedOwn + Block.RewardStakedEdge + Block.RewardStakedShared +
                Block.BonusStakedOwn + Block.BonusStakedEdge + Block.BonusStakedShared;
        }

        public virtual void Revert(L1Block block)
        {
            Cache.Chain.Get().BlocksCount--;

            Db.Blocks.Remove(block);
            Cache.Chain.ReleaseOperationId();
        }

        public async Task RevertRewards(L1Block block)
        {
            var proposer = Cache.Addresses.GetBaker(block.ProposerId!.Value);
            Db.TryAttach(proposer);
            RevertReceiveRewards(proposer, block.RewardDelegated, block.RewardStakedOwn, block.RewardStakedEdge, block.RewardStakedShared);
            proposer.BlocksCount--;

            #region reset baker activity
            if (block.ResetBakerDeactivation != null)
            {
                if (block.ResetBakerDeactivation <= block.Level)
                    await DeactivateBaker(proposer);

                proposer.DeactivationLevel = (int)block.ResetBakerDeactivation;
            }
            #endregion

            var producer = Cache.Addresses.GetBaker(block.ProducerId!.Value);
            Db.TryAttach(producer);
            RevertReceiveRewards(producer, block.BonusDelegated, block.BonusStakedOwn, block.BonusStakedEdge, block.BonusStakedShared);
            if (producer != proposer)
            {
                producer.BlocksCount--;

                #region reset proposer activity
                if (block.ResetProposerDeactivation != null)
                {
                    if (block.ResetProposerDeactivation <= block.Level)
                        await DeactivateBaker(producer);

                    producer.DeactivationLevel = (int)block.ResetProposerDeactivation;
                }
                #endregion
            }
        }

        protected virtual (long, long, long, long, long, long, long, long) ParseRewards(L1Baker proposer, L1Baker producer, List<JsonElement> balanceUpdates)
        {
            var rewardDelegated = 0L;
            var rewardStakedOwn = 0L;
            var bonusDelegated = 0L;
            var bonusStakedOwn = 0L;

            for (int i = 0; i < balanceUpdates.Count; i++)
            {
                var update = balanceUpdates[i];
                if (update.RequiredString("kind") == "minted" && update.RequiredString("category") == "baking rewards")
                {
                    if (i == balanceUpdates.Count - 1)
                        throw new Exception("Unexpected baking rewards balance updates behavior");

                    var change = -update.RequiredInt64("change");

                    var nextUpdate = balanceUpdates[i + 1];
                    if (nextUpdate.RequiredString("kind") == "freezer" &&
                        nextUpdate.RequiredString("category") == "deposits" &&
                        nextUpdate.Required("staker").RequiredString("baker") == proposer.Hash &&
                        nextUpdate.RequiredInt64("change") == change)
                    {
                        if (proposer.ExternalStakedBalance != 0)
                            throw new Exception("Manual staking should be disabled in Oxford");

                        rewardStakedOwn += change;
                    }
                    else if (nextUpdate.RequiredString("kind") == "contract" &&
                        nextUpdate.RequiredString("contract") == proposer.Hash &&
                        nextUpdate.RequiredInt64("change") == change)
                    {
                        rewardDelegated += change;
                    }
                    else
                    {
                        throw new Exception("Unexpected baking rewards balance updates behavior");
                    }
                }
                else if (update.RequiredString("kind") == "minted" && update.RequiredString("category") == "baking bonuses")
                {
                    if (i == balanceUpdates.Count - 1)
                        throw new Exception("Unexpected baking bonuses balance updates behavior");

                    var change = -update.RequiredInt64("change");

                    var nextUpdate = balanceUpdates[i + 1];
                    if (nextUpdate.RequiredString("kind") == "freezer" &&
                        nextUpdate.RequiredString("category") == "deposits" &&
                        nextUpdate.Required("staker").RequiredString("baker") == producer.Hash &&
                        nextUpdate.RequiredInt64("change") == change)
                    {
                        if (producer.ExternalStakedBalance != 0)
                            throw new Exception("Manual staking should be disabled in Oxford");

                        bonusStakedOwn += change;
                    }
                    else if (nextUpdate.RequiredString("kind") == "contract" &&
                        nextUpdate.RequiredString("contract") == producer.Hash &&
                        nextUpdate.RequiredInt64("change") == change)
                    {
                        bonusDelegated += change;
                    }
                    else
                    {
                        throw new Exception("Unexpected baking bonuses balance updates behavior");
                    }
                }
            }

            return (rewardDelegated, rewardStakedOwn, 0L, 0L, bonusDelegated, bonusStakedOwn, 0L, 0L);
        }

        protected virtual long GetAttestationCommittee(L1Protocol protocol, JsonElement metadata) => protocol.AttestersPerBlock;
    }
}
