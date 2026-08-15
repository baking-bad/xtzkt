using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class Diagnostics(ProtocolHandler handler) : Proto16.Diagnostics(handler)
    {
        protected override bool CheckDelegatedBalance(JsonElement remote, L1Baker baker)
        {
            return remote.RequiredInt64("delegated_balance") == baker.ExternalDelegatedBalance + baker.ExternalStakedBalance;
        }

        protected override async Task TestBaker(int level, L1Baker baker, L1Protocol proto)
        {
            await base.TestBaker(level, baker, proto);

            var stakingBalance = await Rpc.GetCurrentStakingBalance(level, baker.Hash);

            if (stakingBalance.RequiredInt64("own_frozen") != baker.OwnStakedBalance)
                throw new Exception($"Diagnostics failed: wrong own_frozen balance for {baker.Hash}");

            if (stakingBalance.RequiredInt64("staked_frozen") != baker.ExternalStakedBalance)
                throw new Exception($"Diagnostics failed: wrong staked_frozen balance for {baker.Hash}");

            if (stakingBalance.RequiredInt64("delegated") != baker.TotalDelegated)
                throw new Exception($"Diagnostics failed: wrong delegated balance for {baker.Hash}");

            if (level > proto.FirstLevel)
            {
                var stakingParameters = await Rpc.GetStakingParameters(level - 1, baker.Hash);

                if (stakingParameters.TryGetProperty("active", out var active))
                {
                    if (active.RequiredInt64("limit_of_staking_over_baking_millionth") != baker.LimitOfStakingOverBaking &&
                        active.RequiredInt64("limit_of_staking_over_baking_millionth") != 2147483647)
                        throw new Exception($"Diagnostics failed: wrong limit_of_staking_over_baking_millionth for {baker.Hash}");

                    if (active.RequiredInt64("edge_of_baking_over_staking_billionth") != baker.EdgeOfBakingOverStaking)
                        throw new Exception($"Diagnostics failed: wrong edge_of_baking_over_staking_billionth for {baker.Hash}");
                }
                else
                {
                    if (baker.LimitOfStakingOverBaking != null || baker.EdgeOfBakingOverStaking != null)
                        throw new Exception($"Diagnostics failed: wrong staking parameters for {baker.Hash}");
                }
            }
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
                
                if (!bakerCycles.TryGetValue(baker.Id, out var bakerCycle))
                    bakerCycle = await Db.BakerCycles.FirstOrDefaultAsync(x => x.ChainId == state.Id && x.Cycle == state.Cycle && x.BakerId == baker.Id);

                if (bakerCycle != null)
                {
                    if ((long)bakerCycle.ExpectedAttestations != remote.RequiredInt64("expected_cycle_activity"))
                        throw new Exception($"Invalid baker ExpectedAttestations {baker.Hash}");

                    if (bakerCycle.FutureAttestationRewards != remote.RequiredInt64("expected_attesting_rewards"))
                    {
                        if (remote.RequiredInt64("expected_attesting_rewards") != 0 || remote.RequiredInt32("expected_cycle_activity") - remote.RequiredInt32("missed_slots") >= remote.RequiredInt32("minimal_cycle_activity"))
                            throw new Exception($"Invalid baker FutureAttestationRewards {baker.Hash}");
                    }

                    if (bakerCycle.MissedAttestations != remote.RequiredInt64("missed_slots"))
                    {
                        var proto = await Cache.Protocols.GetAsync(state.Protocol);
                        if (bakerCycle.Cycle != proto.FirstCycle && bakerCycle.BakingPower > 0)
                            throw new Exception($"Invalid baker MissedAttestations {baker.Hash}");
                    }
                }
                else
                {
                    if (remote.RequiredInt64("expected_cycle_activity") != 0)
                        throw new Exception($"Invalid baker ExpectedAttestations {baker.Hash}");

                    if (remote.RequiredInt64("expected_attesting_rewards") != 0)
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

            if (remote.RequiredArray("selected_stake_distribution").Count() != cycle.TotalBakers)
                throw new Exception($"Invalid cycle {cycle.Index} selected bakers {cycle.TotalBakers}");

            if (remote.Required("total_active_stake").RequiredInt64("frozen") + remote.Required("total_active_stake").RequiredInt64("delegated") != cycle.TotalBakingPower)
                throw new Exception($"Invalid cycle {cycle.Index} selected stake {cycle.TotalBakingPower}");
        }
    }
}
