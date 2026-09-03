using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.L1.Services;

namespace Xtzkt.Indexers.L1.Protocols
{
    public abstract class ProtocolCommit(ProtocolHandler protocol)
    {
        protected readonly XtzktContext Db = protocol.Db;
        protected readonly CacheService Cache = protocol.Cache;
        protected readonly ProtocolHandler Proto = protocol;
        protected readonly BlockContext Context = protocol.Context;
        protected readonly ILogger Logger = protocol.Logger;

        protected L1Baker RegisterBaker(L1User user, L1Protocol? protocol = null)
        {
            var baker = new L1Baker
            {
                ActivationLevel = Context.Block.Level,
                ActivationTimestamp = Context.Block.Timestamp,
                DeactivationLevel = GracePeriod.Init(Context.Block.Level, protocol ?? Context.Protocol),
                Hash = user.Hash,
                FirstLevel = user.FirstLevel,
                FirstTimestamp = user.FirstTimestamp,
                LastLevel = user.LastLevel,
                LastTimestamp = user.LastTimestamp,
                Balance = user.Balance,
                Counter = user.Counter,
                BakerId = null,
                DelegationLevel = null,
                DelegationTimestamp = null,
                Id = user.Id,
                ChainId = user.ChainId,
                Index = user.Index,
                ActivationsCount = user.ActivationsCount,
                DelegationsCount = user.DelegationsCount,
                OriginationsCount = user.OriginationsCount,
                TransactionsCount = user.TransactionsCount,
                RevealsCount = user.RevealsCount,
                RegisterConstantsCount = user.RegisterConstantsCount,
                SetDepositsLimitsCount = user.SetDepositsLimitsCount,
                ContractsCount = user.ContractsCount,
                MigrationsCount = user.MigrationsCount,
                PublicKey = user.PublicKey,
                Revealed = user.Revealed,
                Staked = true,
                OwnDelegatedBalance = user.Balance - user.UnstakedBalance,
                StakedPseudotokens = user.StakedPseudotokens,
                UnstakedBalance = user.UnstakedBalance,
                UnstakedBakerId = user.UnstakedBakerId,
                StakingOpsCount = user.StakingOpsCount,
                SubsidyCount = user.SubsidyCount,
                ExternalDelegatedBalance = 0,
                MinTotalDelegated = long.MaxValue,
                MinTotalDelegatedLevel = 0,
                ActiveTokensCount = user.ActiveTokensCount,
                TokenBalancesCount = user.TokenBalancesCount,
                TokenTransfersCount = user.TokenTransfersCount,
                ActiveTicketsCount = user.ActiveTicketsCount,
                TicketBalancesCount = user.TicketBalancesCount,
                TicketTransfersCount = user.TicketTransfersCount,
                TransferTicketCount = user.TransferTicketCount,
                IncreasePaidStorageCount = user.IncreasePaidStorageCount,
                UpdateSecondaryKeyCount = user.UpdateSecondaryKeyCount,
                DrainDelegateCount = user.DrainDelegateCount,
                SmartRollupBonds = user.SmartRollupBonds,
                SmartRollupsCount = user.SmartRollupsCount,
                SmartRollupAddMessagesCount = user.SmartRollupAddMessagesCount,
                SmartRollupCementCount = user.SmartRollupCementCount,
                SmartRollupExecuteCount = user.SmartRollupExecuteCount,
                SmartRollupOriginateCount = user.SmartRollupOriginateCount,
                SmartRollupPublishCount = user.SmartRollupPublishCount,
                SmartRollupRecoverBondCount = user.SmartRollupRecoverBondCount,
                SmartRollupRefuteCount = user.SmartRollupRefuteCount,
                DalPublishCommitmentOpsCount = user.DalPublishCommitmentOpsCount,
                SetDelegateParametersOpsCount = user.SetDelegateParametersOpsCount,
                RefutationGamesCount = user.RefutationGamesCount,
                ActiveRefutationGamesCount = user.ActiveRefutationGamesCount,
                StakingUpdatesCount = user.StakingUpdatesCount
            };

            UpdateBakerPower(baker);

            Cache.Statistics.Current.TotalOwnDelegated += baker.OwnDelegatedBalance;
            Cache.Statistics.Current.TotalBakers++;

            var isAdded = Db.Entry(user).State == EntityState.Added;
            Db.Entry(user).State = EntityState.Detached;
            Db.Entry(baker).State = isAdded ? EntityState.Added : EntityState.Modified;
            Cache.Addresses.Add(baker);

            return baker;
        }

        protected L1User UnregisterBaker(L1Baker baker)
        {
            var user = new L1User
            {
                Hash = baker.Hash,
                FirstLevel = baker.FirstLevel,
                FirstTimestamp = baker.FirstTimestamp,
                LastLevel = baker.LastLevel,
                LastTimestamp = baker.LastTimestamp,
                Balance = baker.Balance,
                Counter = baker.Counter,
                BakerId = null,
                DelegationLevel = null,
                DelegationTimestamp = null,
                StakedPseudotokens = baker.StakedPseudotokens,
                UnstakedBalance = baker.UnstakedBalance,
                UnstakedBakerId = baker.UnstakedBakerId,
                StakingOpsCount = baker.StakingOpsCount,
                SubsidyCount = baker.SubsidyCount,
                Id = baker.Id,
                ChainId = baker.ChainId,
                Index = baker.Index,
                ActivationsCount = baker.ActivationsCount,
                DelegationsCount = baker.DelegationsCount,
                OriginationsCount = baker.OriginationsCount,
                TransactionsCount = baker.TransactionsCount,
                RevealsCount = baker.RevealsCount,
                RegisterConstantsCount = baker.RegisterConstantsCount,
                SetDepositsLimitsCount = baker.SetDepositsLimitsCount,
                ContractsCount = baker.ContractsCount,
                MigrationsCount = baker.MigrationsCount,
                PublicKey = baker.PublicKey,
                Revealed = baker.Revealed,
                Staked = false,
                ActiveTokensCount = baker.ActiveTokensCount,
                TokenBalancesCount = baker.TokenBalancesCount,
                TokenTransfersCount = baker.TokenTransfersCount,
                ActiveTicketsCount = baker.ActiveTicketsCount,
                TicketBalancesCount = baker.TicketBalancesCount,
                TicketTransfersCount = baker.TicketTransfersCount,
                TransferTicketCount = baker.TransferTicketCount,
                IncreasePaidStorageCount = baker.IncreasePaidStorageCount,
                UpdateSecondaryKeyCount = baker.UpdateSecondaryKeyCount,
                DrainDelegateCount = baker.DrainDelegateCount,
                SmartRollupBonds = baker.SmartRollupBonds,
                SmartRollupsCount = baker.SmartRollupsCount,
                SmartRollupAddMessagesCount = baker.SmartRollupAddMessagesCount,
                SmartRollupCementCount = baker.SmartRollupCementCount,
                SmartRollupExecuteCount = baker.SmartRollupExecuteCount,
                SmartRollupOriginateCount = baker.SmartRollupOriginateCount,
                SmartRollupPublishCount = baker.SmartRollupPublishCount,
                SmartRollupRecoverBondCount = baker.SmartRollupRecoverBondCount,
                SmartRollupRefuteCount = baker.SmartRollupRefuteCount,
                SetDelegateParametersOpsCount = baker.SetDelegateParametersOpsCount,
                DalPublishCommitmentOpsCount = baker.DalPublishCommitmentOpsCount,
                RefutationGamesCount = baker.RefutationGamesCount,
                ActiveRefutationGamesCount = baker.ActiveRefutationGamesCount,
                StakingUpdatesCount = baker.StakingUpdatesCount
            };

            var isAdded = Db.Entry(baker).State == EntityState.Added;
            Db.Entry(baker).State = EntityState.Detached;
            Db.Entry(user).State = isAdded ? EntityState.Added : EntityState.Modified;
            Cache.Addresses.Add(user);

            return user;
        }

        protected async Task ActivateBaker(L1Baker baker)
        {
            if (baker.Staked) return;
            baker.Staked = true;
            
            var delegators = await Db.Addresses
                .OfType<L1Address>()
                .Where(x => x.BakerId != null && x.BakerId == baker.Id)
                .ToListAsync();

            foreach (var delegator in delegators)
            {
                Cache.Addresses.Add(delegator);
                delegator.LastLevel = Context.Block.Level;
                delegator.LastTimestamp = Context.Block.Timestamp;
                delegator.Staked = true;
            }

            UpdateBakerPower(baker);

            Cache.Statistics.Current.TotalOwnStaked += baker.OwnStakedBalance;
            Cache.Statistics.Current.TotalExternalStaked += baker.ExternalStakedBalance;
            Cache.Statistics.Current.TotalOwnDelegated += baker.OwnDelegatedBalance;
            Cache.Statistics.Current.TotalExternalDelegated += baker.ExternalDelegatedBalance;

            Cache.Statistics.Current.TotalBakers++;
            Cache.Statistics.Current.TotalStakers += baker.StakersCount;
            Cache.Statistics.Current.TotalDelegators += baker.DelegatorsCount;
        }

        protected async Task DeactivateBaker(L1Baker baker)
        {
            if (!baker.Staked) return;
            baker.Staked = false;

            var delegators = await Db.Addresses
                .OfType<L1Address>()
                .Where(x => x.BakerId != null && x.BakerId == baker.Id)
                .ToListAsync();

            foreach (var delegator in delegators)
            {
                Cache.Addresses.Add(delegator);
                delegator.LastLevel = Context.Block.Level;
                delegator.LastTimestamp = Context.Block.Timestamp;
                delegator.Staked = false;
            }

            UpdateBakerPower(baker);

            Cache.Statistics.Current.TotalOwnStaked -= baker.OwnStakedBalance;
            Cache.Statistics.Current.TotalExternalStaked -= baker.ExternalStakedBalance;
            Cache.Statistics.Current.TotalOwnDelegated -= baker.OwnDelegatedBalance;
            Cache.Statistics.Current.TotalExternalDelegated -= baker.ExternalDelegatedBalance;

            Cache.Statistics.Current.TotalBakers--;
            Cache.Statistics.Current.TotalStakers -= baker.StakersCount;
            Cache.Statistics.Current.TotalDelegators -= baker.DelegatorsCount;
        }

        protected void UpdateBakersPower()
        {
            foreach (var baker in Cache.Addresses.GetBakers())
            {
                Db.TryAttach(baker);
                UpdateBakerPower(baker);
            }
        }

        protected void UpdateBakerPower(L1Baker baker)
        {
            Cache.Statistics.Current.TotalBakingPower -= baker.BakingPower;
            Cache.Statistics.Current.TotalVotingPower -= baker.VotingPower;
            
            baker.BakingPower = Proto.Helpers.BakingPower(baker);
            baker.VotingPower = Proto.Helpers.VotingPower(baker);

            Cache.Statistics.Current.TotalBakingPower += baker.BakingPower;
            Cache.Statistics.Current.TotalVotingPower += baker.VotingPower;
        }

        protected void RevertBakersPower()
        {
            foreach (var baker in Cache.Addresses.GetBakers())
            {
                Db.TryAttach(baker);
                RevertBakerPower(baker);
            }
        }

        protected void RevertBakerPower(L1Baker baker)
        {
            baker.BakingPower = Proto.Helpers.BakingPower(baker);
            baker.VotingPower = Proto.Helpers.VotingPower(baker);
        }

        protected void ReceiveLockedRewards(L1Baker baker, long amount)
        {
            baker.Balance += amount;
            UpdateBakerPower(baker);
        }

        protected void RevertReceiveLockedRewards(L1Baker baker, long amount)
        {
            baker.Balance -= amount;
            RevertBakerPower(baker);
        }

        protected void BurnLockedRewards(L1Baker baker, long amount)
        {
            baker.Balance -= amount;
            UpdateBakerPower(baker);
        }

        protected void RevertBurnLockedRewards(L1Baker baker, long amount)
        {
            baker.Balance += amount;
            RevertBakerPower(baker);
        }

        protected void UnlockRewards(L1Baker baker, long amount)
        {
            baker.OwnDelegatedBalance += amount;
            UpdateBakerPower(baker);

            if (baker.Staked)
                Cache.Statistics.Current.TotalOwnDelegated += amount;
        }

        protected void RevertUnlockRewards(L1Baker baker, long amount)
        {
            baker.OwnDelegatedBalance -= amount;
            RevertBakerPower(baker);
        }

        protected void PayFee(L1Address address, long bakerFee)
        {
            Spend(address, bakerFee);

            Context.Block.BakerFees += bakerFee;

            Context.Proposer.Balance += bakerFee;
            Context.Proposer.OwnDelegatedBalance += bakerFee;
            UpdateBakerPower(Context.Proposer);

            if (Context.Proposer.Staked)
                Cache.Statistics.Current.TotalOwnDelegated += bakerFee;
        }

        protected void RevertPayFee(L1Address address, long bakerFee)
        {
            RevertSpend(address, bakerFee);

            Context.Proposer.Balance -= bakerFee;
            Context.Proposer.OwnDelegatedBalance -= bakerFee;
            RevertBakerPower(Context.Proposer);
        }

        protected void BurnFee(L1Address address, long burnedFee)
        {
            Spend(address, burnedFee);
            Context.Block.BurnedFees += burnedFee;
            Cache.Statistics.Current.TotalBurned += burnedFee;
        }

        protected void RevertBurnFee(L1Address address, long burnedFee)
        {
            RevertSpend(address, burnedFee);
        }

        protected void BurnFeeAndSpend(L1Address address, long burnedFee, long amount)
        {
            Spend(address, burnedFee + amount);
            Context.Block.BurnedFees += burnedFee;
            Cache.Statistics.Current.TotalBurned += burnedFee;
        }

        protected void RevertBurnFeeAndSpend(L1Address address, long burnedFee, long amount)
        {
            RevertSpend(address, burnedFee + amount);
        }

        protected void Spend(L1Address address, long amount)
        {
            var baker = Cache.Addresses.GetBaker(address.BakerId) ?? address as L1Baker;
            Db.TryAttach(baker);

            Spend(address, baker, amount);
        }

        protected void Spend(L1Address address, L1Baker? baker, long amount)
        {
            address.Balance -= amount;

            if (baker != null)
            {
                if (baker == address)
                {
                    baker.OwnDelegatedBalance -= amount;
                    if (baker.Staked)
                        Cache.Statistics.Current.TotalOwnDelegated -= amount;
                }
                else
                {
                    baker.ExternalDelegatedBalance -= amount;
                    if (baker.Staked)
                        Cache.Statistics.Current.TotalExternalDelegated -= amount;
                }

                UpdateBakerPower(baker);
            }
        }

        protected void RevertSpend(L1Address address, long amount)
        {
            var baker = Cache.Addresses.GetBaker(address.BakerId) ?? address as L1Baker;
            Db.TryAttach(baker);

            RevertSpend(address, baker, amount);
        }

        protected void RevertSpend(L1Address address, L1Baker? baker, long amount)
        {
            address.Balance += amount;

            if (baker != null)
            {
                if (baker == address)
                    baker.OwnDelegatedBalance += amount;
                else
                    baker.ExternalDelegatedBalance += amount;

                RevertBakerPower(baker);
            }
        }

        protected void Receive(L1Address address, long amount)
        {
            var baker = Cache.Addresses.GetBaker(address.BakerId) ?? address as L1Baker;
            Db.TryAttach(baker);

            Receive(address, baker, amount);
        }

        protected void Receive(L1Address address, L1Baker? baker, long amount)
        {
            address.Balance += amount;

            if (baker != null)
            {
                if (baker == address)
                {
                    baker.OwnDelegatedBalance += amount;
                    if (baker.Staked)
                        Cache.Statistics.Current.TotalOwnDelegated += amount;
                }
                else
                {
                    baker.ExternalDelegatedBalance += amount;
                    if (baker.Staked)
                        Cache.Statistics.Current.TotalExternalDelegated += amount;
                }

                UpdateBakerPower(baker);
            }
        }

        protected void RevertReceive(L1Address address, long amount)
        {
            var baker = Cache.Addresses.GetBaker(address.BakerId) ?? address as L1Baker;
            Db.TryAttach(baker);

            RevertReceive(address, baker, amount);
        }

        protected void RevertReceive(L1Address address, L1Baker? baker, long amount)
        {
            address.Balance -= amount;

            if (baker != null)
            {
                if (baker == address)
                    baker.OwnDelegatedBalance -= amount;
                else
                    baker.ExternalDelegatedBalance -= amount;

                RevertBakerPower(baker);
            }
        }

        protected void ReceiveRewards(L1Baker baker, long delegated, long stakedOwn, long stakedEdge, long stakedShared)
        {
            baker.Balance += delegated + stakedOwn + stakedEdge;
            baker.OwnDelegatedBalance += delegated;
            baker.OwnStakedBalance += stakedOwn + stakedEdge;
            baker.ExternalStakedBalance += stakedShared;
            UpdateBakerPower(baker);

            if (baker.Staked)
            {
                Cache.Statistics.Current.TotalOwnDelegated += delegated;
                Cache.Statistics.Current.TotalOwnStaked += stakedOwn + stakedEdge;
                Cache.Statistics.Current.TotalExternalStaked += stakedShared;
            }
        }

        protected void RevertReceiveRewards(L1Baker baker, long delegated, long stakedOwn, long stakedEdge, long stakedShared)
        {
            baker.Balance -= delegated + stakedOwn + stakedEdge;
            baker.OwnDelegatedBalance -= delegated;
            baker.OwnStakedBalance -= stakedOwn + stakedEdge;
            baker.ExternalStakedBalance -= stakedShared;
            RevertBakerPower(baker);
        }

        protected void Delegate(L1Address delegator, L1Baker baker, int delegationLevel, DateTime delegationTimestamp)
        {
            var amount = delegator.Balance - ((delegator as L1User)?.UnstakedBalance ?? 0);

            delegator.BakerId = baker.Id;
            delegator.DelegationLevel = delegationLevel;
            delegator.DelegationTimestamp = delegationTimestamp;
            delegator.Staked = baker.Staked;

            baker.DelegatorsCount++;
            baker.ExternalDelegatedBalance += amount;

            UpdateBakerPower(baker);

            if (baker.Staked)
            {
                Cache.Statistics.Current.TotalExternalDelegated += amount;
                Cache.Statistics.Current.TotalDelegators++;
            }
        }

        protected void Undelegate(L1Address delegator, L1Baker baker)
        {
            var amount = delegator.Balance - ((delegator as L1User)?.UnstakedBalance ?? 0);

            delegator.BakerId = null;
            delegator.DelegationLevel = null;
            delegator.DelegationTimestamp = null;
            delegator.Staked = false;

            baker.DelegatorsCount--;
            baker.ExternalDelegatedBalance -= amount;

            UpdateBakerPower(baker);

            if (baker.Staked)
            {
                Cache.Statistics.Current.TotalExternalDelegated -= amount;
                Cache.Statistics.Current.TotalDelegators--;
            }
        }
    }
}
