using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto02
{
    class RevelationPenaltyCommit : ProtocolCommit
    {
        public RevelationPenaltyCommit(ProtocolHandler protocol) : base(protocol) { }

        public virtual async Task Apply(L1Block block, JsonElement rawBlock)
        {
            #region init
            List<RevelationPenaltyOperation>? revelationPenalties = null;

            if (block.Events.HasFlag(L1BlockEvents.CycleEnd))
            {
                if (HasPenaltiesUpdates(block, Context.Protocol, rawBlock))
                {
                    revelationPenalties = [];

                    var missedBlocks = await Db.L1Blocks
                        .Join(Db.L1Protocols, x => x.ProtocolId, x => x.Id, (block, protocol) => new { block, protocol })
                        .Where(x => x.block.ChainId == block.ChainId &&
                            x.block.Level % x.protocol.BlocksPerCommitment == 0 &&
                            x.block.Cycle == block.Cycle - 1 &&
                            x.block.RevelationId == null)
                        .Select(x => x.block)
                        .ToListAsync();

                    var penalizedBakers = missedBlocks
                        .Select(x => x.ProposerId)
                        .ToHashSet();

                    var bakerCycles = await Db.BakerCycles.AsNoTracking()
                        .Where(x => x.ChainId == block.ChainId && x.Cycle == block.Cycle - 1 && penalizedBakers.Contains(x.BakerId))
                        .ToListAsync();

                    var slashedBakers = bakerCycles
                        .Where(x => x.DoubleBakingLostStaked > 0 || x.DoubleConsensusLostStaked > 0)
                        .Select(x => x.BakerId)
                        .ToHashSet();

                    foreach (var missedBlock in missedBlocks)
                    {
                        var missedBlockProposer = Cache.Addresses.GetBaker(missedBlock.ProposerId!.Value);
                        var slashed = slashedBakers.Contains(missedBlockProposer.Id);
                        revelationPenalties.Add(new RevelationPenaltyOperation
                        {
                            Id = Cache.Chain.NextOperationId(),
                            ChainId = block.ChainId,
                            BakerId = missedBlockProposer.Id,
                            Level = block.Level,
                            Timestamp = block.Timestamp,
                            MissedLevel = missedBlock.Level,
                            Loss = slashed ? 0 : missedBlock.RewardDelegated + missedBlock.BakerFees
                        });
                    }
                }
            }
            #endregion

            if (revelationPenalties == null) return;

            foreach (var penalty in revelationPenalties)
            {
                #region entities
                var baker = Cache.Addresses.GetBaker(penalty.BakerId);
                Db.TryAttach(baker);
                #endregion

                if (penalty.Loss != 0)
                {
                    var lostFees = (await Cache.Blocks.GetAsync(penalty.MissedLevel)).BakerFees;
                    Spend(baker, baker, lostFees);
                    BurnLockedRewards(baker, penalty.Loss - lostFees);
                }

                baker.RevelationPenaltiesCount++;
                block.Operations |= L1Operations.RevelationPenalty;

                Cache.Chain.Get().RevelationPenaltyOpsCount++;
                Cache.Statistics.Current.TotalBurned += penalty.Loss;
                Cache.Statistics.Current.TotalFrozen -= penalty.Loss;

                Db.RevelationPenaltyOps.Add(penalty);
                Context.RevelationPenaltyOps.Add(penalty);
            }
        }

        public virtual async Task Revert(L1Block block)
        {
            foreach (var penalty in Context.RevelationPenaltyOps)
            {
                #region entities
                var baker = Cache.Addresses.GetBaker(penalty.BakerId);
                Db.TryAttach(baker);
                #endregion

                if (penalty.Loss != 0)
                {
                    var lostFees = (await Cache.Blocks.GetAsync(penalty.MissedLevel)).BakerFees;
                    RevertSpend(baker, baker, lostFees);
                    RevertBurnLockedRewards(baker, penalty.Loss - lostFees);
                }

                baker.RevelationPenaltiesCount--;

                Cache.Chain.Get().RevelationPenaltyOpsCount--;

                Db.RevelationPenaltyOps.Remove(penalty);
                Cache.Chain.ReleaseOperationId();
            }
        }

        protected virtual int GetFreezerCycle(JsonElement el) => el.RequiredInt32("level");

        protected virtual bool HasPenaltiesUpdates(L1Block block, L1Protocol protocol, JsonElement rawBlock)
        {
            return rawBlock
                .Required("metadata")
                .RequiredArray("balance_updates")
                .EnumerateArray()
                .Any(x => x.RequiredString("kind")[0] == 'f' &&
                          x.RequiredInt64("change") < 0 &&
                          GetFreezerCycle(x) != block.Cycle - protocol.ConsensusRightsDelay);
        }
    }
}
