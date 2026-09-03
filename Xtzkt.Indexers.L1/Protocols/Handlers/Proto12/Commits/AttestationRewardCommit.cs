using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class AttestationRewardCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement rawBlock)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            var losses = rawBlock.Required("metadata").RequiredArray("balance_updates").EnumerateArray()
                .Where(x => x.RequiredString("origin") == "block" &&
                            x.RequiredString("kind") == "burned" &&
                            x.RequiredString("category") == "lost endorsing rewards")
                .ToDictionary(x => Cache.Addresses.GetExistingBaker(x.RequiredString("delegate")).Id, x => x.RequiredInt64("change"));

            var bakerCycles = await Cache.BakerCycles.GetAsync(block.Cycle);
            var ops = new List<AttestationRewardOperation>(bakerCycles.Count);

            foreach (var (bakerId, bakerCycle) in bakerCycles.Where(x => x.Value.FutureAttestationRewards > 0))
            {
                ops.Add(new()
                {
                    Id = Cache.Chain.NextOperationId(),
                    ChainId = block.ChainId,
                    BakerId = bakerId,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                    Expected = bakerCycle.FutureAttestationRewards,
                    RewardDelegated = bakerCycle.FutureAttestationRewards
                });

                Db.TryAttach(bakerCycle);
                if (losses.TryGetValue(bakerId, out var loss))
                {
                    if (bakerCycle.FutureAttestationRewards != loss)
                        throw new Exception("FutureAttestationRewards != loss");

                    ops[^1].RewardDelegated = 0; 
                    bakerCycle.MissedAttestationRewards += bakerCycle.FutureAttestationRewards;
                    bakerCycle.FutureAttestationRewards = 0;
                }
                else
                {
                    bakerCycle.AttestationRewardsDelegated += bakerCycle.FutureAttestationRewards;
                    bakerCycle.FutureAttestationRewards = 0;
                }
            }

            foreach (var op in ops)
            {
                var baker = Cache.Addresses.GetBaker(op.BakerId);
                Db.TryAttach(baker);

                Receive(baker, baker, op.RewardDelegated);
                baker.AttestationRewardsCount++;

                block.Operations |= L1Operations.AttestationRewards;

                Cache.Statistics.Current.TotalCreated += op.RewardDelegated;
            }

            Cache.Chain.Get().AttestationRewardOpsCount += ops.Count;

            Db.AttestationRewardOps.AddRange(ops);
            Context.AttestationRewardOps.AddRange(ops);
        }

        public virtual async Task Revert(L1Block block)
        {
            if (Context.AttestationRewardOps.Count == 0)
                return;

            foreach (var op in Context.AttestationRewardOps)
            {
                var baker = Cache.Addresses.GetBaker(op.BakerId);
                Db.TryAttach(baker);

                RevertReceive(baker, baker, op.RewardDelegated);
                baker.AttestationRewardsCount--;

                var bakerCycle = await Cache.BakerCycles.GetAsync(block.Cycle, baker.Id);
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureAttestationRewards = op.Expected;
                if (op.Expected == op.RewardDelegated)
                    bakerCycle.AttestationRewardsDelegated -= op.Expected;
                else
                    bakerCycle.MissedAttestationRewards -= op.Expected;
            }

            Cache.Chain.Get().AttestationRewardOpsCount -= Context.AttestationRewardOps.Count;

            Db.AttestationRewardOps.RemoveRange(Context.AttestationRewardOps);
            Cache.Chain.ReleaseOperationId(Context.AttestationRewardOps.Count);
        }
    }
}
