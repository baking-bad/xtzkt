using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto22
{
    class DalAttestationRewardCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task Apply(L1Block block, JsonElement rawBlock)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            var state = Cache.Chain.Get();
            var bakerCycles = await Cache.BakerCycles.GetAsync(block.Cycle);
            var ops = bakerCycles
                .Where(x => x.Value.FutureDalAttestationRewards > 0)
                .ToDictionary(
                    x => x.Key,
                    bakerCycle => new DalAttestationRewardOperation
                    {
                        Id = Cache.Chain.NextOperationId(),
                        ChainId = block.ChainId,
                        BakerId = bakerCycle.Key,
                        Level = block.Level,
                        Timestamp = block.Timestamp,
                        Expected = bakerCycle.Value.FutureDalAttestationRewards
                    });

            var balanceUpdates = rawBlock.Required("metadata").RequiredArray("balance_updates").EnumerateArray().Where(x => x.RequiredString("origin") == "block").ToList();
            for (int i = 0; i < balanceUpdates.Count; i++)
            {
                var update = balanceUpdates[i];
                if (update.RequiredString("kind") == "minted" && update.RequiredString("category") == "DAL attesting rewards")
                {
                    if (i == balanceUpdates.Count - 1)
                        throw new Exception("Unexpected DAL attestation rewards balance updates behavior");

                    var change = -update.RequiredInt64("change");

                    var nextUpdate = balanceUpdates[i + 1];
                    if (nextUpdate.RequiredInt64("change") != change)
                        throw new Exception("Unexpected DAL attestation rewards balance updates behavior");

                    if (nextUpdate.RequiredString("kind") == "freezer" && nextUpdate.RequiredString("category") == "deposits")
                    {
                        var staker = nextUpdate.Required("staker");
                        if (staker.TryGetProperty("baker_own_stake", out var p))
                        {
                            var baker = Cache.Addresses.GetExistingBaker(p.RequiredString());
                            if (!ops.TryGetValue(baker.Id, out var op))
                                throw new Exception("Unexpected DAL attestation rewards balance update");

                            op.RewardStakedOwn += change;
                        }
                        else if (staker.TryGetProperty("baker_edge", out p))
                        {
                            var baker = Cache.Addresses.GetExistingBaker(p.RequiredString());
                            if (!ops.TryGetValue(baker.Id, out var op))
                                throw new Exception("Unexpected DAL attestation rewards balance update");

                            op.RewardStakedEdge += change;
                        }
                        else if (staker.TryGetProperty("delegate", out p))
                        {
                            var baker = Cache.Addresses.GetExistingBaker(p.RequiredString());
                            if (!ops.TryGetValue(baker.Id, out var op))
                                throw new Exception("Unexpected DAL attestation rewards balance update");

                            op.RewardStakedShared += change;
                        }
                        else
                        {
                            throw new Exception("Unexpected DAL attestation rewards balance updates behavior");
                        }
                    }
                    else if (nextUpdate.RequiredString("kind") == "contract")
                    {
                        var baker = Cache.Addresses.GetExistingBaker(nextUpdate.RequiredString("contract"));
                        if (!ops.TryGetValue(baker.Id, out var op))
                            throw new Exception("Unexpected DAL attestation rewards balance update");

                        op.RewardDelegated = change;
                    }
                    else if (nextUpdate.RequiredString("kind") == "burned" && nextUpdate.RequiredString("category") == "lost DAL attesting rewards")
                    {
                        var baker = Cache.Addresses.GetExistingBaker(nextUpdate.RequiredString("delegate"));
                        if (!ops.TryGetValue(baker.Id, out var op))
                            throw new Exception("Unexpected DAL attestation rewards balance update");

                        if (op.Expected != change)
                            throw new Exception("FutureDalAttestationRewards != loss");

                        op.RewardDelegated = 0;
                        op.RewardStakedOwn = 0;
                        op.RewardStakedEdge = 0;
                        op.RewardStakedShared = 0;
                    }
                    else
                    {
                        throw new Exception("Unexpected DAL attestation rewards balance updates behavior");
                    }
                }
            }

            foreach (var op in ops.Values)
            {
                var bakerCycle = bakerCycles[op.BakerId];
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureDalAttestationRewards = 0;
                if (op.RewardDelegated != 0 || op.RewardStakedOwn != 0 || op.RewardStakedEdge != 0 || op.RewardStakedShared != 0)
                {
                    if (op.Expected != op.RewardDelegated + op.RewardStakedOwn + op.RewardStakedEdge + op.RewardStakedShared)
                        throw new Exception("ExpectedReward != RewardFrozen + RewardDelegated");

                    bakerCycle.DalAttestationRewardsDelegated = op.RewardDelegated;
                    bakerCycle.DalAttestationRewardsStakedOwn = op.RewardStakedOwn;
                    bakerCycle.DalAttestationRewardsStakedEdge = op.RewardStakedEdge;
                    bakerCycle.DalAttestationRewardsStakedShared = op.RewardStakedShared;
                }
                else
                {
                    bakerCycle.MissedDalAttestationRewards = op.Expected;
                }


                var baker = Cache.Addresses.GetBaker(op.BakerId);
                Db.TryAttach(baker);

                ReceiveRewards(baker, op.RewardDelegated, op.RewardStakedOwn, op.RewardStakedEdge, op.RewardStakedShared);
                baker.DalAttestationRewardsCount++;

                block.Operations |= L1Operations.DalAttestationReward;

                Cache.Statistics.Current.TotalCreated += op.RewardDelegated + op.RewardStakedOwn + op.RewardStakedEdge + op.RewardStakedShared;
                Cache.Statistics.Current.TotalFrozen += op.RewardStakedOwn + op.RewardStakedEdge + op.RewardStakedShared;
            }

            Cache.Chain.Get().DalAttestationRewardOpsCount += ops.Count;

            Db.DalAttestationRewardOps.AddRange(ops.Values);
            Context.DalAttestationRewardOps.AddRange(ops.Values);
        }

        public virtual async Task Revert(L1Block block)
        {
            if (!block.Operations.HasFlag(L1Operations.DalAttestationReward))
                return;

            var ops = await Db.DalAttestationRewardOps.Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            foreach (var op in ops)
            {
                var bakerCycle = await Cache.BakerCycles.GetAsync(block.Cycle, op.BakerId);
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureDalAttestationRewards = op.Expected;
                bakerCycle.MissedDalAttestationRewards = 0;
                bakerCycle.DalAttestationRewardsDelegated = 0;
                bakerCycle.DalAttestationRewardsStakedOwn = 0;
                bakerCycle.DalAttestationRewardsStakedEdge = 0;
                bakerCycle.DalAttestationRewardsStakedShared = 0;

                var baker = Cache.Addresses.GetBaker(op.BakerId);
                Db.TryAttach(baker);

                RevertReceiveRewards(baker, op.RewardDelegated, op.RewardStakedOwn, op.RewardStakedEdge, op.RewardStakedShared);
                baker.DalAttestationRewardsCount--;
            }

            Cache.Chain.Get().DalAttestationRewardOpsCount -= ops.Count;

            Db.DalAttestationRewardOps.RemoveRange(ops);
            Cache.Chain.ReleaseOperationId(ops.Count);
        }
    }
}
