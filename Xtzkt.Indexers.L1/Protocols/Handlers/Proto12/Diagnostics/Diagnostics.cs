using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class Diagnostics(ProtocolHandler handler) : Proto05.Diagnostics(handler)
    {
        protected override async Task TestBaker(int level, L1Baker baker, L1Protocol proto)
        {
            var remote = await Rpc.GetDelegateAsync(level, baker.Hash);

            if (!CheckFullBalance(remote, baker))
                throw new Exception($"Diagnostics failed: wrong balance {baker.Hash}");
            
            if (!CheckStakingBalance(remote, baker))
                throw new Exception($"Diagnostics failed: wrong staking balance {baker.Hash}");

            if (!CheckDelegatedBalance(remote, baker))
                throw new Exception($"Diagnostics failed: wrong delegated balance {baker.Hash}");

            if (!CheckMinDelegatedBalance(remote, baker))
                throw new Exception($"Diagnostics failed: wrong min delegated balance {baker.Hash}");

            if (!CheckBakingPower(remote, baker))
                throw new Exception($"Diagnostics failed: wrong baking power {baker.Hash}");

            if (!CheckVotingPower(remote, baker))
                throw new Exception($"Diagnostics failed: wrong voting power {baker.Hash}");

            if (remote.RequiredBool("deactivated") != !baker.Staked)
                throw new Exception($"Diagnostics failed: wrong deactivation state {baker.Hash}");

            var deactivationCycle = (baker.DeactivationLevel - 1) >= proto.FirstLevel
                ? proto.GetCycle(baker.DeactivationLevel - 1)
                : (await Cache.Blocks.GetAsync(baker.DeactivationLevel - 1)).Cycle;

            if (remote.RequiredInt32("grace_period") != deactivationCycle)
                throw new Exception($"Diagnostics failed: wrong grace period {baker.Hash}");

            if (!CheckFrozenDepositLimit(remote, baker))
                throw new Exception($"Diagnostics failed: wrong frozen deposits limit {baker.Hash}");
            
            TestDelegatorsCount(remote, baker);
        }

        protected override async Task TestParticipation(L1Chain state)
        {
            var bakers = Cache.Addresses.GetBakers().ToList();
            var bakerCycles = Db.ChangeTracker.Entries()
                .Where(x => x.Entity is BakerCycle bc && bc.Cycle == state.Cycle)
                .Select(x => (x.Entity as BakerCycle)!)
                .ToDictionary(x => x.BakerId);

            foreach (var baker in bakers)
            {
                var remote = await Rpc.GetDelegateParticipationAsync(state.Level, baker.Hash);
                
                if (bakerCycles.TryGetValue(baker.Id, out var bakerCycle))
                {
                    if ((long)bakerCycle.ExpectedAttestations != remote.RequiredInt64("expected_cycle_activity"))
                        throw new Exception($"Invalid baker ExpectedAttestations {baker.Hash}");

                    if (bakerCycle.FutureAttestationRewards != remote.RequiredInt64("expected_endorsing_rewards"))
                        throw new Exception($"Invalid baker FutureAttestationRewards {baker.Hash}");

                    if (bakerCycle.MissedAttestations != remote.RequiredInt64("missed_slots"))
                    {
                        var proto = await Cache.Protocols.GetAsync(state.Protocol);
                        if (bakerCycle.Cycle != proto.FirstCycle)
                            throw new Exception($"Invalid baker MissedAttestations {baker.Hash}");
                    }
                }
                else
                {
                    if (remote.RequiredInt64("expected_cycle_activity") != 0)
                        throw new Exception($"Invalid baker ExpectedAttestations {baker.Hash}");

                    if (remote.RequiredInt64("expected_endorsing_rewards") != 0)
                        throw new Exception($"Invalid baker FutureAttestationRewards {baker.Hash}");

                    if (remote.RequiredInt64("missed_slots") != 0)
                        throw new Exception($"Invalid baker MissedAttestations {baker.Hash}");
                }
            }
        }
        
        protected override async Task TestCycle(L1Chain state, Cycle cycle)
        {
            var level = Math.Min(state.Level, cycle.FirstLevel);
            var remote = await Rpc.GetCycleAsync(level, cycle.Index);
                
            if (remote.RequiredString("random_seed") != Hex.Convert(cycle.Seed))
                throw new Exception($"Invalid cycle {cycle.Index} seed {Hex.Convert(cycle.Seed)}");

            if (remote.RequiredInt64("total_active_stake") != cycle.TotalBakingPower)
                throw new Exception($"Invalid cycle {cycle.Index} selected stake {cycle.TotalBakingPower}");

            if (remote.RequiredArray("selected_stake_distribution").Count() != cycle.TotalBakers)
                throw new Exception($"Invalid cycle {cycle.Index} selected bakers {cycle.TotalBakers}");
        }

        protected virtual bool CheckFullBalance(JsonElement remote, L1Baker baker) =>
            remote.RequiredInt64("full_balance") == baker.Balance;

        protected virtual bool CheckStakingBalance(JsonElement remote, L1Baker baker) =>
            remote.RequiredInt64("staking_balance") == baker.TotalDelegated + baker.TotalStaked;

        protected virtual bool CheckDelegatedBalance(JsonElement remote, L1Baker baker) =>
            remote.RequiredInt64("delegated_balance") == baker.ExternalDelegatedBalance;

        protected virtual bool CheckMinDelegatedBalance(JsonElement remote, L1Baker baker) => true;

        protected virtual bool CheckFrozenDepositLimit(JsonElement remote, L1Baker baker) =>
            remote.OptionalInt64("frozen_deposits_limit") == baker.FrozenDepositLimit;

        protected virtual bool CheckBakingPower(JsonElement remote, L1Baker baker)
        {
            return true;
        }

        protected virtual bool CheckVotingPower(JsonElement remote, L1Baker baker)
        {
            return true;
        }
    }
}
