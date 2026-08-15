using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto21
{
    class Diagnostics(ProtocolHandler handler) : Proto18.Diagnostics(handler)
    {
        protected override bool CheckMinDelegatedBalance(JsonElement remote, L1Baker baker)
        {
            var minDelegated = remote.Required("min_delegated_in_current_cycle");
            return minDelegated.RequiredInt64("amount") == baker.MinTotalDelegated &&
                minDelegated.Required("level").RequiredInt32("level") == baker.MinTotalDelegatedLevel;
        }

        protected override bool CheckFullBalance(JsonElement remote, L1Baker baker)
        {
            return remote.RequiredInt64("own_full_balance") == baker.Balance;
        }

        protected override bool CheckStakingBalance(JsonElement remote, L1Baker baker)
        {
            return remote.RequiredInt64("total_staked") == baker.TotalStaked && remote.RequiredInt64("total_delegated") == baker.TotalDelegated;
        }

        protected override void TestDelegatorsCount(JsonElement remote, L1Baker local)
        {
            var delegators = remote.RequiredArray("delegators").Count();
            if (delegators != local.DelegatorsCount && delegators != local.DelegatorsCount + 1)
                throw new Exception($"Diagnostics failed: wrong delegators count {local.Hash}");
        }

        protected override bool CheckFrozenDepositLimit(JsonElement remote, L1Baker baker)
        {
            return true;
        }

        protected override bool CheckDelegatedBalance(JsonElement remote, L1Baker baker)
        {
            return remote.RequiredInt64("external_delegated") == baker.ExternalDelegatedBalance;
        }

        protected override bool CheckBakingPower(JsonElement remote, L1Baker baker)
        {
            var externalStakeCap = 0L;
            if (baker.LimitOfStakingOverBaking is long limit)
            {
                var (q, r) = Math.DivRem(limit, 1_000_000);
                if (r == 0)
                {
                    externalStakeCap = baker.OwnStakedBalance * Math.Min(q, Context.Protocol.MaxExternalOverOwnStakeRatio);
                }
                else
                {
                    var limitOfStakingOverBaking = Math.Min(limit, Context.Protocol.MaxExternalOverOwnStakeRatio * 1_000_000);
                    externalStakeCap = baker.OwnStakedBalance.MulRatio(limitOfStakingOverBaking, 1_000_000);
                }
            }
            var overstaked = Math.Max(0, baker.ExternalStakedBalance - externalStakeCap);
            var totalDelegated = remote.Required("min_delegated_in_current_cycle").RequiredInt64("amount") + overstaked;
            var delegationCap = baker.OwnStakedBalance * Context.Protocol.MaxDelegatedOverFrozenRatio;

            var actualStaked = baker.OwnStakedBalance + baker.ExternalStakedBalance - overstaked;
            var actualDelegated = Math.Min(totalDelegated, delegationCap);

            var state = Cache.Chain.Get();
            if (state.AiActivationLevel is int aiLevel && state.Level >= aiLevel)
                actualDelegated /= Context.Protocol.StakePowerMultiplier;

            var uncheckedBakingPower = actualStaked + actualDelegated;
            return uncheckedBakingPower == remote.RequiredInt64("baking_power");
        }

        protected override bool CheckVotingPower(JsonElement remote, L1Baker baker)
        {
            var uncheckedVotingPower = baker.TotalDelegated + baker.TotalStaked;
            return uncheckedVotingPower == remote.RequiredInt64("current_voting_power");
        }
    }
}
