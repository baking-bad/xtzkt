using Netezos.Encoding;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
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

            if (level % protocol.BlocksPerSnapshot == 0)
                events |= L1BlockEvents.BalanceSnapshot;

            var payloadRound = header.RequiredInt32("payload_round");
            var blockRound = Hex.Parse(header.RequiredArray("fitness", 5)[4].RequiredString()).ToInt32();
            var balanceUpdates = metadata.RequiredArray("balance_updates").EnumerateArray();
            var rewardUpdate = balanceUpdates.FirstOrDefault(x => x.RequiredString("kind") == "minted" && x.RequiredString("category") == "baking rewards");
            var bonusUpdate = balanceUpdates.FirstOrDefault(x => x.RequiredString("kind") == "minted" && x.RequiredString("category") == "baking bonuses");

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
                AttestationCommittee = protocol.AttestersPerBlock,
                PayloadRound = payloadRound,
                BlockRound = blockRound,
                ProposerId = proposer.Id,
                ProducerId = producer.Id,
                Events = events,
                RewardDelegated = rewardUpdate.ValueKind == JsonValueKind.Undefined ? 0 : -rewardUpdate.RequiredInt64("change"),
                BonusDelegated = bonusUpdate.ValueKind == JsonValueKind.Undefined ? 0 : -bonusUpdate.RequiredInt64("change"),
                LBToggle = GetLBToggleVote(rawBlock),
                LBToggleEma = GetLBToggleEma(rawBlock)
            };

            Context.Block = Block;
            Context.Proposer = proposer;
            Context.Protocol = protocol;

            Db.TryAttach(proposer);
            Receive(proposer, proposer, Block.RewardDelegated);
            proposer.BlocksCount++;

            #region set baker active
            var newDeactivationLevel = proposer.Staked ? GracePeriod.Reset(Block.Level, protocol) : GracePeriod.Init(Block.Level, protocol);
            if (proposer.DeactivationLevel < newDeactivationLevel)
            {
                if (proposer.DeactivationLevel <= Block.Level)
                    await ActivateBaker(proposer);

                Block.ResetBakerDeactivation = proposer.DeactivationLevel;
                proposer.DeactivationLevel = newDeactivationLevel;
            }
            #endregion

            Db.TryAttach(producer);
            Receive(producer, producer, Block.BonusDelegated);
            if (producer.Id != proposer.Id)
            {
                producer.BlocksCount++;

                #region set proposer active
                newDeactivationLevel = producer.Staked ? GracePeriod.Reset(Block.Level, protocol) : GracePeriod.Init(Block.Level, protocol);
                if (producer.DeactivationLevel < newDeactivationLevel)
                {
                    if (producer.DeactivationLevel <= Block.Level)
                        await ActivateBaker(producer);

                    Block.ResetProposerDeactivation = producer.DeactivationLevel;
                    producer.DeactivationLevel = newDeactivationLevel;
                }
                #endregion
            }

            Db.TryAttach(protocol); // if we don't attach it, ef will recognize it as 'added'
            if (Block.Events.HasFlag(L1BlockEvents.ProtocolEnd))
                protocol.LastLevel = Block.Level;


            Cache.Chain.Get().BlocksCount++;
            Cache.Statistics.Current.TotalCreated += Block.RewardDelegated + Block.BonusDelegated;

            Db.Blocks.Add(Block);
            Cache.Blocks.Add(Block);
        }

        public virtual async Task Revert(L1Block block)
        {
            Block = block;

            var proposer = Context.Proposer;
            Db.TryAttach(proposer);
            RevertReceive(proposer, proposer, Block.RewardDelegated);
            proposer.BlocksCount--;

            #region reset baker activity
            if (Block.ResetBakerDeactivation != null)
            {
                if (Block.ResetBakerDeactivation <= Block.Level)
                    await DeactivateBaker(proposer);

                proposer.DeactivationLevel = (int)Block.ResetBakerDeactivation;
            }
            #endregion

            var producer = Cache.Addresses.GetBaker(block.ProducerId!.Value);
            Db.TryAttach(producer);
            RevertReceive(producer, producer, Block.BonusDelegated);
            if (producer.Id != proposer.Id)
            {
                producer.BlocksCount--;

                #region reset proposer activity
                if (Block.ResetProposerDeactivation != null)
                {
                    if (Block.ResetProposerDeactivation <= Block.Level)
                        await DeactivateBaker(producer);

                    producer.DeactivationLevel = (int)Block.ResetProposerDeactivation;
                }
                #endregion
            }

            Cache.Chain.Get().BlocksCount--;

            Db.Blocks.Remove(Block);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual bool? GetLBToggleVote(JsonElement block)
            => !block.Required("header").RequiredBool("liquidity_baking_escape_vote");

        protected virtual int GetLBToggleEma(JsonElement block)
            => block.Required("metadata").RequiredInt32("liquidity_baking_escape_ema") * 1000;
    }
}
