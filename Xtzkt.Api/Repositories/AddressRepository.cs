using Dapper;
using Npgsql;
using System.Numerics;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class AddressRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    SoftwareCache _softwareCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",         (@"""Id""",         "integer") },
        { "firstLevel", (@"""FirstLevel""", "integer") },
        { "firstTimestamp", (@"""FirstTimestamp""", "timestamptz") },
        { "lastLevel",  (@"""LastLevel""",  "integer") },
        { "lastTimestamp",  (@"""LastTimestamp""",  "timestamptz") },
    };

    bool ProcessFilters(AddressFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        return filter.Chain.Id!.Eq != -1;
    }

    async Task<IEnumerable<dynamic>> Query(AddressFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        if (selection != null)
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "type":                        columns.Add(@"""Type"""); break;
                    case "id":                          columns.Add(@"""Id"""); break;
                    case "chain":                       columns.Add(@"""ChainId"""); break;
                    case "hash":                        columns.Add(@"""Hash"""); break;
                    case "layer":                       columns.Add(@"""Layer"""); break;
                    case "runtime":                     columns.Add(@"""Runtime"""); break;
                    case "firstLevel":                  columns.Add(@"""FirstLevel"""); break;
                    case "firstTimestamp":              columns.Add(@"""FirstTimestamp"""); break;
                    case "lastLevel":                   columns.Add(@"""LastLevel"""); break;
                    case "lastTimestamp":               columns.Add(@"""LastTimestamp"""); break;
                    case "contractsCount":              columns.Add(@"""ContractsCount"""); break;
                    case "activeTokensCount":           columns.Add(@"""ActiveTokensCount"""); break;
                    case "tokenBalancesCount":          columns.Add(@"""TokenBalancesCount"""); break;
                    case "tokenTransfersCount":         columns.Add(@"""TokenTransfersCount"""); break;
                    case "activeTicketsCount":          columns.Add(@"""ActiveTicketsCount"""); break;
                    case "ticketBalancesCount":         columns.Add(@"""TicketBalancesCount"""); break;
                    case "ticketTransfersCount":        columns.Add(@"""TicketTransfersCount"""); break;
                    case "transactionsCount":           columns.Add(@"""TransactionsCount"""); break;
                    case "originationsCount":           columns.Add(@"""OriginationsCount"""); break;
                    case "migrationsCount":             columns.Add(@"""MigrationsCount"""); break;
                    // L1/XMichelson use "Balance"; XEvm uses "Balance18"
                    case "balance":                     columns.Add(@"""Balance"""); columns.Add(@"""Balance18"""); break;
                    // XAddress
                    case "aliasesCount":                columns.Add(@"""AliasesCount"""); break;
                    case "depositOpsCount":             columns.Add(@"""DepositOpsCount"""); break;
                    // L1AddressBase
                    case "smartRollupBonds":            columns.Add(@"""SmartRollupBonds"""); break;
                    case "counter":                     columns.Add(@"""Counter"""); break;
                    case "baker":                       columns.Add(@"""BakerId"""); break;
                    case "delegationLevel":             columns.Add(@"""DelegationLevel"""); break;
                    case "delegationTimestamp":         columns.Add(@"""DelegationTimestamp"""); break;
                    case "staked":                      columns.Add(@"""Staked"""); break;
                    case "index":                       columns.Add(@"""Index"""); break;
                    case "smartRollupsCount":           columns.Add(@"""SmartRollupsCount"""); break;
                    case "delegationsCount":            columns.Add(@"""DelegationsCount"""); break;
                    case "revealsCount":                columns.Add(@"""RevealsCount"""); break;
                    case "transferTicketCount":         columns.Add(@"""TransferTicketCount"""); break;
                    case "increasePaidStorageCount":    columns.Add(@"""IncreasePaidStorageCount"""); break;
                    case "updateSecondaryKeyCount":     columns.Add(@"""UpdateSecondaryKeyCount"""); break;
                    case "drainDelegateCount":          columns.Add(@"""DrainDelegateCount"""); break;
                    case "subsidyCount":                columns.Add(@"""SubsidyCount"""); break;
                    case "smartRollupAddMessagesCount": columns.Add(@"""SmartRollupAddMessagesCount"""); break;
                    case "smartRollupCementCount":      columns.Add(@"""SmartRollupCementCount"""); break;
                    case "smartRollupExecuteCount":     columns.Add(@"""SmartRollupExecuteCount"""); break;
                    case "smartRollupOriginateCount":   columns.Add(@"""SmartRollupOriginateCount"""); break;
                    case "smartRollupPublishCount":     columns.Add(@"""SmartRollupPublishCount"""); break;
                    case "smartRollupRecoverBondCount": columns.Add(@"""SmartRollupRecoverBondCount"""); break;
                    case "smartRollupRefuteCount":      columns.Add(@"""SmartRollupRefuteCount"""); break;
                    case "refutationGamesCount":        columns.Add(@"""RefutationGamesCount"""); break;
                    case "activeRefutationGamesCount":  columns.Add(@"""ActiveRefutationGamesCount"""); break;
                    // L1User / XMichelsonUser
                    case "revealed":                    columns.Add(@"""Revealed"""); break;
                    case "publicKey":                   columns.Add(@"""PublicKey"""); break;
                    case "registerConstantsCount":      columns.Add(@"""RegisterConstantsCount"""); break;
                    // L1User
                    case "stakedPseudotokens":          columns.Add(@"""StakedPseudotokens"""); break;
                    case "unstakedBalance":             columns.Add(@"""UnstakedBalance"""); break;
                    case "unstakedBaker":               columns.Add(@"""UnstakedBakerId"""); break;
                    case "stakingUpdatesCount":         columns.Add(@"""StakingUpdatesCount"""); break;
                    case "activationsCount":            columns.Add(@"""ActivationsCount"""); break;
                    case "setDepositsLimitsCount":      columns.Add(@"""SetDepositsLimitsCount"""); break;
                    case "stakingOpsCount":             columns.Add(@"""StakingOpsCount"""); break;
                    case "setDelegateParametersOpsCount":  columns.Add(@"""SetDelegateParametersOpsCount"""); break;
                    case "dalPublishCommitmentOpsCount":   columns.Add(@"""DalPublishCommitmentOpsCount"""); break;
                    // L1Baker
                    case "activationLevel":             columns.Add(@"""ActivationLevel"""); break;
                    case "activationTimestamp":         columns.Add(@"""ActivationTimestamp"""); break;
                    case "deactivationLevel":           columns.Add(@"""DeactivationLevel"""); break;
                    case "consensusAddress":            columns.Add(@"""ConsensusAddress"""); break;
                    case "companionAddress":            columns.Add(@"""CompanionAddress"""); break;
                    case "bakingPower":                 columns.Add(@"""BakingPower"""); break;
                    case "votingPower":                 columns.Add(@"""VotingPower"""); break;
                    case "ownDelegatedBalance":         columns.Add(@"""OwnDelegatedBalance"""); break;
                    case "externalDelegatedBalance":    columns.Add(@"""ExternalDelegatedBalance"""); break;
                    case "minTotalDelegated":           columns.Add(@"""MinTotalDelegated"""); break;
                    case "minTotalDelegatedLevel":      columns.Add(@"""MinTotalDelegatedLevel"""); break;
                    case "delegatorsCount":             columns.Add(@"""DelegatorsCount"""); break;
                    case "ownStakedBalance":            columns.Add(@"""OwnStakedBalance"""); break;
                    case "externalStakedBalance":       columns.Add(@"""ExternalStakedBalance"""); break;
                    case "issuedPseudotokens":          columns.Add(@"""IssuedPseudotokens"""); break;
                    case "stakersCount":                columns.Add(@"""StakersCount"""); break;
                    case "externalUnstakedBalance":     columns.Add(@"""ExternalUnstakedBalance"""); break;
                    case "roundingError":               columns.Add(@"""RoundingError"""); break;
                    case "frozenDepositLimit":          columns.Add(@"""FrozenDepositLimit"""); break;
                    case "limitOfStakingOverBaking":    columns.Add(@"""LimitOfStakingOverBaking"""); break;
                    case "edgeOfBakingOverStaking":     columns.Add(@"""EdgeOfBakingOverStaking"""); break;
                    case "blocksCount":                 columns.Add(@"""BlocksCount"""); break;
                    case "attestationsCount":           columns.Add(@"""AttestationsCount"""); break;
                    case "preattestationsCount":        columns.Add(@"""PreattestationsCount"""); break;
                    case "ballotsCount":                columns.Add(@"""BallotsCount"""); break;
                    case "proposalsCount":              columns.Add(@"""ProposalsCount"""); break;
                    case "dalEntrapmentEvidenceOpsCount": columns.Add(@"""DalEntrapmentEvidenceOpsCount"""); break;
                    case "doubleBakingCount":           columns.Add(@"""DoubleBakingCount"""); break;
                    case "doubleConsensusCount":        columns.Add(@"""DoubleConsensusCount"""); break;
                    case "nonceRevelationsCount":       columns.Add(@"""NonceRevelationsCount"""); break;
                    case "vdfRevelationsCount":         columns.Add(@"""VdfRevelationsCount"""); break;
                    case "revelationPenaltiesCount":    columns.Add(@"""RevelationPenaltiesCount"""); break;
                    case "attestationRewardsCount":     columns.Add(@"""AttestationRewardsCount"""); break;
                    case "dalAttestationRewardsCount":  columns.Add(@"""DalAttestationRewardsCount"""); break;
                    case "autostakingOpsCount":         columns.Add(@"""AutostakingOpsCount"""); break;
                    case "software":                    columns.Add(@"""SoftwareId"""); break;
                    case "softwareUpdateLevel":         columns.Add(@"""SoftwareUpdateLevel"""); break;
                    // L1Contract / XMichelsonContract / XEvmContract
                    case "kind":        columns.Add(@"""Kind"""); break;
                    case "typeHash":    columns.Add(@"""TypeHash"""); break;
                    case "codeHash":    columns.Add(@"""CodeHash"""); break;
                    case "creator":     columns.Add(@"""CreatorId"""); break;
                    case "logsCount":   columns.Add(@"""LogsCount"""); break;
                    case "tokensCount":   columns.Add(@"""TokensCount"""); break;
                    case "tags":        columns.Add(@"""Tags"""); break;
                    case "ticketsCount":columns.Add(@"""TicketsCount"""); break;
                    // L1SmartRollup
                    case "pvmKind":             columns.Add(@"""PvmKind"""); break;
                    case "parameterSchema":     columns.Add(@"""ParameterSchema"""); break;
                    case "genesisCommitment":   columns.Add(@"""GenesisCommitment"""); break;
                    case "lastCommitment":      columns.Add(@"""LastCommitment"""); break;
                    case "inboxLevel":          columns.Add(@"""InboxLevel"""); break;
                    case "totalStakers":        columns.Add(@"""TotalStakers"""); break;
                    case "activeStakers":       columns.Add(@"""ActiveStakers"""); break;
                    case "executedCommitments": columns.Add(@"""ExecutedCommitments"""); break;
                    case "cementedCommitments": columns.Add(@"""CementedCommitments"""); break;
                    case "pendingCommitments":  columns.Add(@"""PendingCommitments"""); break;
                    case "refutedCommitments":  columns.Add(@"""RefutedCommitments"""); break;
                    case "orphanCommitments":   columns.Add(@"""OrphanCommitments"""); break;
                    // XEvm
                    case "eip7702DelegationCount": columns.Add(@"""Eip7702DelegationCount"""); break;
                    case "eip7702Delegate":        columns.Add(@"""Eip7702DelegateId"""); break;
                    case "activeBridgeTicketsCount": columns.Add(@"""ActiveBridgeTicketsCount"""); break;
                    case "bridgeTicketBalancesCount": columns.Add(@"""BridgeTicketBalancesCount"""); break;
                    case "bridgeTicketTransfersCount": columns.Add(@"""BridgeTicketTransfersCount"""); break;
                    // XEvmAlias / XMichelsonAlias
                    case "owner":   columns.Add(@"""OwnerId"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Addresses""")
            .Where(@"""Id""", filter.Id)
            .Where(@"""ChainId""", filter.Chain?.Id)
            .Where(@"""Hash""", filter.Hash)
            .Where(@"""Type""", filter.Type)
            .Where(@"""Layer""", filter.Layer)
            .Where(@"""Runtime""", filter.Runtime)
            .Where(@"""FirstLevel""", filter.FirstLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastLevel""", filter.LastLevel)
            .Where(@"""LastTimestamp""", filter.LastTimestamp)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(AddressFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.AddressCounter);

        if (!ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Addresses""")
            .Where(@"""Id""", filter.Id)
            .Where(@"""ChainId""", filter.Chain?.Id)
            .Where(@"""Hash""", filter.Hash)
            .Where(@"""Type""", filter.Type)
            .Where(@"""Layer""", filter.Layer)
            .Where(@"""Runtime""", filter.Runtime)
            .Where(@"""FirstLevel""", filter.FirstLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastLevel""", filter.LastLevel)
            .Where(@"""LastTimestamp""", filter.LastTimestamp)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public Address Get(Data.Models.Address address)
    {
        return address switch
        {
            Data.Models.L1Baker row => new L1Baker
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.L1,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                Balance = row.Balance,
                SmartRollupBonds = row.SmartRollupBonds,
                Counter = row.Counter,
                Baker = _addressCache.GetInfo(row.BakerId),
                DelegationLevel = row.DelegationLevel,
                DelegationTimestamp = row.DelegationTimestamp,
                Staked = row.Staked,
                Index = row.Index,
                SmartRollupsCount = row.SmartRollupsCount,
                DelegationsCount = row.DelegationsCount,
                RevealsCount = row.RevealsCount,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                DrainDelegateCount = row.DrainDelegateCount,
                SubsidyCount = row.SubsidyCount,
                SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                SmartRollupCementCount = row.SmartRollupCementCount,
                SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                SmartRollupPublishCount = row.SmartRollupPublishCount,
                SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                RefutationGamesCount = row.RefutationGamesCount,
                ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                Revealed = row.Revealed,
                PublicKey = row.PublicKey,
                StakedPseudotokens = row.StakedPseudotokens,
                UnstakedBalance = row.UnstakedBalance,
                UnstakedBaker = _addressCache.GetInfo(row.UnstakedBakerId),
                StakingUpdatesCount = row.StakingUpdatesCount,
                ActivationsCount = row.ActivationsCount,
                RegisterConstantsCount = row.RegisterConstantsCount,
                SetDepositsLimitsCount = row.SetDepositsLimitsCount,
                StakingOpsCount = row.StakingOpsCount,
                SetDelegateParametersOpsCount = row.SetDelegateParametersOpsCount,
                DalPublishCommitmentOpsCount = row.DalPublishCommitmentOpsCount,
                ActivationLevel = row.ActivationLevel,
                ActivationTimestamp = row.ActivationTimestamp,
                DeactivationLevel = row.DeactivationLevel,
                ConsensusAddress = row.ConsensusAddress,
                CompanionAddress = row.CompanionAddress,
                BakingPower = row.BakingPower,
                VotingPower = row.VotingPower,
                OwnDelegatedBalance = row.OwnDelegatedBalance,
                ExternalDelegatedBalance = row.ExternalDelegatedBalance,
                MinTotalDelegated = row.MinTotalDelegated,
                MinTotalDelegatedLevel = row.MinTotalDelegatedLevel,
                DelegatorsCount = row.DelegatorsCount,
                OwnStakedBalance = row.OwnStakedBalance,
                ExternalStakedBalance = row.ExternalStakedBalance,
                IssuedPseudotokens = row.IssuedPseudotokens,
                StakersCount = row.StakersCount,
                ExternalUnstakedBalance = row.ExternalUnstakedBalance,
                RoundingError = row.RoundingError,
                FrozenDepositLimit = row.FrozenDepositLimit,
                LimitOfStakingOverBaking = row.LimitOfStakingOverBaking,
                EdgeOfBakingOverStaking = row.EdgeOfBakingOverStaking,
                BlocksCount = row.BlocksCount,
                AttestationsCount = row.AttestationsCount,
                PreattestationsCount = row.PreattestationsCount,
                BallotsCount = row.BallotsCount,
                ProposalsCount = row.ProposalsCount,
                DalEntrapmentEvidenceOpsCount = row.DalEntrapmentEvidenceOpsCount,
                DoubleBakingCount = row.DoubleBakingCount,
                DoubleConsensusCount = row.DoubleConsensusCount,
                NonceRevelationsCount = row.NonceRevelationsCount,
                VdfRevelationsCount = row.VdfRevelationsCount,
                RevelationPenaltiesCount = row.RevelationPenaltiesCount,
                AttestationRewardsCount = row.AttestationRewardsCount,
                DalAttestationRewardsCount = row.DalAttestationRewardsCount,
                AutostakingOpsCount = row.AutostakingOpsCount,
                Software = _softwareCache.GetInfo(row.SoftwareId),
                SoftwareUpdateLevel = row.SoftwareUpdateLevel,
            },
            Data.Models.L1User row => new L1User
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.L1,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                Balance = row.Balance,
                SmartRollupBonds = row.SmartRollupBonds,
                Counter = row.Counter,
                Baker = _addressCache.GetInfo(row.BakerId),
                DelegationLevel = row.DelegationLevel,
                DelegationTimestamp = row.DelegationTimestamp,
                Staked = row.Staked,
                Index = row.Index,
                SmartRollupsCount = row.SmartRollupsCount,
                DelegationsCount = row.DelegationsCount,
                RevealsCount = row.RevealsCount,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                DrainDelegateCount = row.DrainDelegateCount,
                SubsidyCount = row.SubsidyCount,
                SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                SmartRollupCementCount = row.SmartRollupCementCount,
                SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                SmartRollupPublishCount = row.SmartRollupPublishCount,
                SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                RefutationGamesCount = row.RefutationGamesCount,
                ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                Revealed = row.Revealed,
                PublicKey = row.PublicKey,
                StakedPseudotokens = row.StakedPseudotokens,
                UnstakedBalance = row.UnstakedBalance,
                UnstakedBaker = _addressCache.GetInfo(row.UnstakedBakerId),
                StakingUpdatesCount = row.StakingUpdatesCount,
                ActivationsCount = row.ActivationsCount,
                RegisterConstantsCount = row.RegisterConstantsCount,
                SetDepositsLimitsCount = row.SetDepositsLimitsCount,
                StakingOpsCount = row.StakingOpsCount,
                SetDelegateParametersOpsCount = row.SetDelegateParametersOpsCount,
                DalPublishCommitmentOpsCount = row.DalPublishCommitmentOpsCount,
            },
            Data.Models.L1Contract row => new L1Contract
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.L1,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                Balance = row.Balance,
                SmartRollupBonds = row.SmartRollupBonds,
                Counter = row.Counter,
                Baker = _addressCache.GetInfo(row.BakerId),
                DelegationLevel = row.DelegationLevel,
                DelegationTimestamp = row.DelegationTimestamp,
                Staked = row.Staked,
                Index = row.Index,
                SmartRollupsCount = row.SmartRollupsCount,
                DelegationsCount = row.DelegationsCount,
                RevealsCount = row.RevealsCount,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                DrainDelegateCount = row.DrainDelegateCount,
                SubsidyCount = row.SubsidyCount,
                SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                SmartRollupCementCount = row.SmartRollupCementCount,
                SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                SmartRollupPublishCount = row.SmartRollupPublishCount,
                SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                RefutationGamesCount = row.RefutationGamesCount,
                ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                Kind = ContractKinds.ToString((int)row.Kind),
                TypeHash = row.TypeHash,
                CodeHash = row.CodeHash,
                Tags = ContractTags.ToList((int)row.Tags),
                TokensCount = row.TokensCount,
                LogsCount = row.LogsCount,
                TicketsCount = row.TicketsCount,
                Creator = _addressCache.GetInfo(row.CreatorId),
            },
            Data.Models.L1SmartRollup row => new L1SmartRollup
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.L1,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                Balance = row.Balance,
                SmartRollupBonds = row.SmartRollupBonds,
                Counter = row.Counter,
                Baker = _addressCache.GetInfo(row.BakerId),
                DelegationLevel = row.DelegationLevel,
                DelegationTimestamp = row.DelegationTimestamp,
                Staked = row.Staked,
                Index = row.Index,
                SmartRollupsCount = row.SmartRollupsCount,
                DelegationsCount = row.DelegationsCount,
                RevealsCount = row.RevealsCount,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                DrainDelegateCount = row.DrainDelegateCount,
                SubsidyCount = row.SubsidyCount,
                SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                SmartRollupCementCount = row.SmartRollupCementCount,
                SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                SmartRollupPublishCount = row.SmartRollupPublishCount,
                SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                RefutationGamesCount = row.RefutationGamesCount,
                ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                Creator = _addressCache.GetInfo(row.CreatorId),
                PvmKind = PvmKinds.ToString((int)row.PvmKind),
                ParameterSchema = row.ParameterSchema,
                GenesisCommitment = row.GenesisCommitment,
                LastCommitment = row.LastCommitment,
                InboxLevel = row.InboxLevel,
                TotalStakers = row.TotalStakers,
                ActiveStakers = row.ActiveStakers,
                ExecutedCommitments = row.ExecutedCommitments,
                CementedCommitments = row.CementedCommitments,
                PendingCommitments = row.PendingCommitments,
                RefutedCommitments = row.RefutedCommitments,
                OrphanCommitments = row.OrphanCommitments,
            },
            Data.Models.L1Ghost row => new L1Ghost
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.L1,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                Balance = row.Balance,
                SmartRollupBonds = row.SmartRollupBonds,
                Counter = row.Counter,
                Baker = _addressCache.GetInfo(row.BakerId),
                DelegationLevel = row.DelegationLevel,
                DelegationTimestamp = row.DelegationTimestamp,
                Staked = row.Staked,
                Index = row.Index,
                SmartRollupsCount = row.SmartRollupsCount,
                DelegationsCount = row.DelegationsCount,
                RevealsCount = row.RevealsCount,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                DrainDelegateCount = row.DrainDelegateCount,
                SubsidyCount = row.SubsidyCount,
                SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                SmartRollupCementCount = row.SmartRollupCementCount,
                SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                SmartRollupPublishCount = row.SmartRollupPublishCount,
                SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                RefutationGamesCount = row.RefutationGamesCount,
                ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
            },
            Data.Models.XEvmUser row => new XEvmUser
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.TezosX,
                Runtime = Runtimes.Evm,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                AliasesCount = row.AliasesCount,
                DepositOpsCount = row.DepositOpsCount,
                ActiveBridgeTicketsCount = row.ActiveBridgeTicketsCount,
                BridgeTicketBalancesCount = row.BridgeTicketBalancesCount,
                BridgeTicketTransfersCount = row.BridgeTicketTransfersCount,
                Counter = row.Counter,
                Balance = row.Balance,
                BlocksCount = row.BlocksCount,
                Eip7702DelegationCount = row.Eip7702DelegationCount,
                LogsCount = row.LogsCount,
                Eip7702Delegate = _addressCache.GetInfo(row.Eip7702DelegateId),
            },
            Data.Models.XEvmAlias row => new XEvmAlias
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.TezosX,
                Runtime = Runtimes.Evm,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                AliasesCount = row.AliasesCount,
                DepositOpsCount = row.DepositOpsCount,
                ActiveBridgeTicketsCount = row.ActiveBridgeTicketsCount,
                BridgeTicketBalancesCount = row.BridgeTicketBalancesCount,
                BridgeTicketTransfersCount = row.BridgeTicketTransfersCount,
                Counter = row.Counter,
                Balance = row.Balance,
                BlocksCount = row.BlocksCount,
                Eip7702DelegationCount = row.Eip7702DelegationCount,
                LogsCount = row.LogsCount,
                Owner = _addressCache.GetInfo(row.OwnerId),
                Eip7702Delegate = _addressCache.GetInfo(row.Eip7702DelegateId),
            },
            Data.Models.XEvmContract row => new XEvmContract
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.TezosX,
                Runtime = Runtimes.Evm,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                AliasesCount = row.AliasesCount,
                DepositOpsCount = row.DepositOpsCount,
                ActiveBridgeTicketsCount = row.ActiveBridgeTicketsCount,
                BridgeTicketBalancesCount = row.BridgeTicketBalancesCount,
                BridgeTicketTransfersCount = row.BridgeTicketTransfersCount,
                Counter = row.Counter,
                Balance = row.Balance,
                BlocksCount = row.BlocksCount,
                Eip7702DelegationCount = row.Eip7702DelegationCount,
                TypeHash = row.TypeHash,
                CodeHash = row.CodeHash,
                Creator = _addressCache.GetInfo(row.CreatorId),
                LogsCount = row.LogsCount,
                TokensCount = row.TokensCount,
                Kind = ContractKinds.ToString((int)row.Kind),
                Tags = ContractTags.ToList((int)row.Tags),
            },
            Data.Models.XMichelsonUser row => new XMichelsonUser
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.TezosX,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                AliasesCount = row.AliasesCount,
                DepositOpsCount = row.DepositOpsCount,
                Balance = row.Balance,
                Index = row.Index,
                RevealsCount = row.RevealsCount,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                RegisterConstantsCount = row.RegisterConstantsCount,
                Counter = row.Counter,
                Revealed = row.Revealed,
                PublicKey = row.PublicKey,
            },
            Data.Models.XMichelsonAlias row => new XMichelsonAlias
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.TezosX,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                AliasesCount = row.AliasesCount,
                DepositOpsCount = row.DepositOpsCount,
                Balance = row.Balance,
                Index = row.Index,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                Owner = _addressCache.GetInfo(row.OwnerId),
            },
            Data.Models.XMichelsonContract row => new XMichelsonContract
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.TezosX,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                AliasesCount = row.AliasesCount,
                DepositOpsCount = row.DepositOpsCount,
                Balance = row.Balance,
                Index = row.Index,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                Kind = ContractKinds.ToString((int)row.Kind),
                TypeHash = row.TypeHash,
                CodeHash = row.CodeHash,
                Tags = ContractTags.ToList((int)row.Tags),
                TokensCount = row.TokensCount,
                LogsCount = row.LogsCount,
                TicketsCount = row.TicketsCount,
                Creator = _addressCache.GetInfo(row.CreatorId),
            },
            Data.Models.XMichelsonGhost row => new XMichelsonGhost
            {
                Id = row.Id,
                Chain = _chainCache.GetInfo(row.ChainId),
                Hash = row.Hash,
                Layer = Layers.TezosX,
                Runtime = Runtimes.Michelson,
                FirstLevel = row.FirstLevel,
                FirstTimestamp = row.FirstTimestamp,
                LastLevel = row.LastLevel,
                LastTimestamp = row.LastTimestamp,
                ContractsCount = row.ContractsCount,
                ActiveTokensCount = row.ActiveTokensCount,
                TokenBalancesCount = row.TokenBalancesCount,
                TokenTransfersCount = row.TokenTransfersCount,
                ActiveTicketsCount = row.ActiveTicketsCount,
                TicketBalancesCount = row.TicketBalancesCount,
                TicketTransfersCount = row.TicketTransfersCount,
                TransactionsCount = row.TransactionsCount,
                OriginationsCount = row.OriginationsCount,
                MigrationsCount = row.MigrationsCount,
                AliasesCount = row.AliasesCount,
                DepositOpsCount = row.DepositOpsCount,
                Balance = row.Balance,
                Index = row.Index,
                TransferTicketCount = row.TransferTicketCount,
                IncreasePaidStorageCount = row.IncreasePaidStorageCount,
            },
            _ => throw new InvalidOperationException("Failed to read Address")
        };
    }

    public async Task<IEnumerable<Address>> Get(AddressFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, Address>(row =>
        {
            return (Data.Models.AddressType)row.Type switch
            {
                Data.Models.AddressType.L1User => new L1User
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.L1,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    Balance = row.Balance,
                    SmartRollupBonds = row.SmartRollupBonds,
                    Counter = row.Counter,
                    Baker = _addressCache.GetInfo((int?)row.BakerId),
                    DelegationLevel = row.DelegationLevel,
                    DelegationTimestamp = row.DelegationTimestamp,
                    Staked = row.Staked,
                    Index = row.Index,
                    SmartRollupsCount = row.SmartRollupsCount,
                    DelegationsCount = row.DelegationsCount,
                    RevealsCount = row.RevealsCount,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                    DrainDelegateCount = row.DrainDelegateCount,
                    SubsidyCount = row.SubsidyCount,
                    SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                    SmartRollupCementCount = row.SmartRollupCementCount,
                    SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                    SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                    SmartRollupPublishCount = row.SmartRollupPublishCount,
                    SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                    SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                    RefutationGamesCount = row.RefutationGamesCount,
                    ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                    Revealed = row.Revealed,
                    PublicKey = row.PublicKey,
                    StakedPseudotokens = row.StakedPseudotokens == null ? null : (BigInteger)row.StakedPseudotokens,
                    UnstakedBalance = row.UnstakedBalance,
                    UnstakedBaker = _addressCache.GetInfo((int?)row.UnstakedBakerId),
                    StakingUpdatesCount = row.StakingUpdatesCount,
                    ActivationsCount = row.ActivationsCount,
                    RegisterConstantsCount = row.RegisterConstantsCount,
                    SetDepositsLimitsCount = row.SetDepositsLimitsCount,
                    StakingOpsCount = row.StakingOpsCount,
                    SetDelegateParametersOpsCount = row.SetDelegateParametersOpsCount,
                    DalPublishCommitmentOpsCount = row.DalPublishCommitmentOpsCount,
                },
                Data.Models.AddressType.L1Baker => new L1Baker
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.L1,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    Balance = row.Balance,
                    SmartRollupBonds = row.SmartRollupBonds,
                    Counter = row.Counter,
                    Baker = _addressCache.GetInfo((int?)row.BakerId),
                    DelegationLevel = row.DelegationLevel,
                    DelegationTimestamp = row.DelegationTimestamp,
                    Staked = row.Staked,
                    Index = row.Index,
                    SmartRollupsCount = row.SmartRollupsCount,
                    DelegationsCount = row.DelegationsCount,
                    RevealsCount = row.RevealsCount,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                    DrainDelegateCount = row.DrainDelegateCount,
                    SubsidyCount = row.SubsidyCount,
                    SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                    SmartRollupCementCount = row.SmartRollupCementCount,
                    SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                    SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                    SmartRollupPublishCount = row.SmartRollupPublishCount,
                    SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                    SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                    RefutationGamesCount = row.RefutationGamesCount,
                    ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                    Revealed = row.Revealed,
                    PublicKey = row.PublicKey,
                    StakedPseudotokens = row.StakedPseudotokens == null ? null : (BigInteger)row.StakedPseudotokens,
                    UnstakedBalance = row.UnstakedBalance,
                    UnstakedBaker = _addressCache.GetInfo((int?)row.UnstakedBakerId),
                    StakingUpdatesCount = row.StakingUpdatesCount,
                    ActivationsCount = row.ActivationsCount,
                    RegisterConstantsCount = row.RegisterConstantsCount,
                    SetDepositsLimitsCount = row.SetDepositsLimitsCount,
                    StakingOpsCount = row.StakingOpsCount,
                    SetDelegateParametersOpsCount = row.SetDelegateParametersOpsCount,
                    DalPublishCommitmentOpsCount = row.DalPublishCommitmentOpsCount,
                    ActivationLevel = row.ActivationLevel,
                    ActivationTimestamp = row.ActivationTimestamp,
                    DeactivationLevel = row.DeactivationLevel,
                    ConsensusAddress = row.ConsensusAddress,
                    CompanionAddress = row.CompanionAddress,
                    BakingPower = row.BakingPower,
                    VotingPower = row.VotingPower,
                    OwnDelegatedBalance = row.OwnDelegatedBalance,
                    ExternalDelegatedBalance = row.ExternalDelegatedBalance,
                    MinTotalDelegated = row.MinTotalDelegated,
                    MinTotalDelegatedLevel = row.MinTotalDelegatedLevel,
                    DelegatorsCount = row.DelegatorsCount,
                    OwnStakedBalance = row.OwnStakedBalance,
                    ExternalStakedBalance = row.ExternalStakedBalance,
                    IssuedPseudotokens = row.IssuedPseudotokens == null ? null : (BigInteger)row.IssuedPseudotokens,
                    StakersCount = row.StakersCount,
                    ExternalUnstakedBalance = row.ExternalUnstakedBalance,
                    RoundingError = row.RoundingError,
                    FrozenDepositLimit = row.FrozenDepositLimit,
                    LimitOfStakingOverBaking = row.LimitOfStakingOverBaking,
                    EdgeOfBakingOverStaking = row.EdgeOfBakingOverStaking,
                    BlocksCount = row.BlocksCount,
                    AttestationsCount = row.AttestationsCount,
                    PreattestationsCount = row.PreattestationsCount,
                    BallotsCount = row.BallotsCount,
                    ProposalsCount = row.ProposalsCount,
                    DalEntrapmentEvidenceOpsCount = row.DalEntrapmentEvidenceOpsCount,
                    DoubleBakingCount = row.DoubleBakingCount,
                    DoubleConsensusCount = row.DoubleConsensusCount,
                    NonceRevelationsCount = row.NonceRevelationsCount,
                    VdfRevelationsCount = row.VdfRevelationsCount,
                    RevelationPenaltiesCount = row.RevelationPenaltiesCount,
                    AttestationRewardsCount = row.AttestationRewardsCount,
                    DalAttestationRewardsCount = row.DalAttestationRewardsCount,
                    AutostakingOpsCount = row.AutostakingOpsCount,
                    Software = _softwareCache.GetInfo((int?)row.SoftwareId),
                    SoftwareUpdateLevel = row.SoftwareUpdateLevel,
                },
                Data.Models.AddressType.L1Contract => new L1Contract
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.L1,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    Balance = row.Balance,
                    SmartRollupBonds = row.SmartRollupBonds,
                    Counter = row.Counter,
                    Baker = _addressCache.GetInfo((int?)row.BakerId),
                    DelegationLevel = row.DelegationLevel,
                    DelegationTimestamp = row.DelegationTimestamp,
                    Staked = row.Staked,
                    Index = row.Index,
                    SmartRollupsCount = row.SmartRollupsCount,
                    DelegationsCount = row.DelegationsCount,
                    RevealsCount = row.RevealsCount,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                    DrainDelegateCount = row.DrainDelegateCount,
                    SubsidyCount = row.SubsidyCount,
                    SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                    SmartRollupCementCount = row.SmartRollupCementCount,
                    SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                    SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                    SmartRollupPublishCount = row.SmartRollupPublishCount,
                    SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                    SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                    RefutationGamesCount = row.RefutationGamesCount,
                    ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                    Kind = ContractKinds.ToString((int)row.Kind),
                    TypeHash = row.TypeHash,
                    CodeHash = row.CodeHash,
                    Tags = ContractTags.ToList((int)row.Tags),
                    TokensCount = row.TokensCount,
                    LogsCount = row.LogsCount,
                    TicketsCount = row.TicketsCount,
                    Creator = _addressCache.GetInfo((int)row.CreatorId),
                },
                Data.Models.AddressType.L1SmartRollup => new L1SmartRollup
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.L1,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    Balance = row.Balance,
                    SmartRollupBonds = row.SmartRollupBonds,
                    Counter = row.Counter,
                    Baker = _addressCache.GetInfo((int?)row.BakerId),
                    DelegationLevel = row.DelegationLevel,
                    DelegationTimestamp = row.DelegationTimestamp,
                    Staked = row.Staked,
                    Index = row.Index,
                    SmartRollupsCount = row.SmartRollupsCount,
                    DelegationsCount = row.DelegationsCount,
                    RevealsCount = row.RevealsCount,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                    DrainDelegateCount = row.DrainDelegateCount,
                    SubsidyCount = row.SubsidyCount,
                    SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                    SmartRollupCementCount = row.SmartRollupCementCount,
                    SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                    SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                    SmartRollupPublishCount = row.SmartRollupPublishCount,
                    SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                    SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                    RefutationGamesCount = row.RefutationGamesCount,
                    ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                    Creator = _addressCache.GetInfo((int)row.CreatorId),
                    PvmKind = PvmKinds.ToString((int)row.PvmKind),
                    ParameterSchema = row.ParameterSchema,
                    GenesisCommitment = row.GenesisCommitment,
                    LastCommitment = row.LastCommitment,
                    InboxLevel = row.InboxLevel,
                    TotalStakers = row.TotalStakers,
                    ActiveStakers = row.ActiveStakers,
                    ExecutedCommitments = row.ExecutedCommitments,
                    CementedCommitments = row.CementedCommitments,
                    PendingCommitments = row.PendingCommitments,
                    RefutedCommitments = row.RefutedCommitments,
                    OrphanCommitments = row.OrphanCommitments,
                },
                Data.Models.AddressType.L1Ghost => new L1Ghost
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.L1,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    Balance = row.Balance,
                    SmartRollupBonds = row.SmartRollupBonds,
                    Counter = row.Counter,
                    Baker = _addressCache.GetInfo((int?)row.BakerId),
                    DelegationLevel = row.DelegationLevel,
                    DelegationTimestamp = row.DelegationTimestamp,
                    Staked = row.Staked,
                    Index = row.Index,
                    SmartRollupsCount = row.SmartRollupsCount,
                    DelegationsCount = row.DelegationsCount,
                    RevealsCount = row.RevealsCount,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    UpdateSecondaryKeyCount = row.UpdateSecondaryKeyCount,
                    DrainDelegateCount = row.DrainDelegateCount,
                    SubsidyCount = row.SubsidyCount,
                    SmartRollupAddMessagesCount = row.SmartRollupAddMessagesCount,
                    SmartRollupCementCount = row.SmartRollupCementCount,
                    SmartRollupExecuteCount = row.SmartRollupExecuteCount,
                    SmartRollupOriginateCount = row.SmartRollupOriginateCount,
                    SmartRollupPublishCount = row.SmartRollupPublishCount,
                    SmartRollupRecoverBondCount = row.SmartRollupRecoverBondCount,
                    SmartRollupRefuteCount = row.SmartRollupRefuteCount,
                    RefutationGamesCount = row.RefutationGamesCount,
                    ActiveRefutationGamesCount = row.ActiveRefutationGamesCount,
                },
                Data.Models.AddressType.XEvmUser => new XEvmUser
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.TezosX,
                    Runtime = Runtimes.Evm,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    AliasesCount = row.AliasesCount,
                    DepositOpsCount = row.DepositOpsCount,
                    ActiveBridgeTicketsCount = row.ActiveBridgeTicketsCount,
                    BridgeTicketBalancesCount = row.BridgeTicketBalancesCount,
                    BridgeTicketTransfersCount = row.BridgeTicketTransfersCount,
                    Counter = row.Counter,
                    Balance = (BigInteger)row.Balance18,
                    BlocksCount = row.BlocksCount,
                    Eip7702DelegationCount = row.Eip7702DelegationCount,
                    LogsCount = row.LogsCount,
                    Eip7702Delegate = _addressCache.GetInfo((int?)row.Eip7702DelegateId),
                },
                Data.Models.AddressType.XEvmAlias => new XEvmAlias
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.TezosX,
                    Runtime = Runtimes.Evm,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    AliasesCount = row.AliasesCount,
                    DepositOpsCount = row.DepositOpsCount,
                    ActiveBridgeTicketsCount = row.ActiveBridgeTicketsCount,
                    BridgeTicketBalancesCount = row.BridgeTicketBalancesCount,
                    BridgeTicketTransfersCount = row.BridgeTicketTransfersCount,
                    Counter = row.Counter,
                    Balance = (BigInteger)row.Balance18,
                    BlocksCount = row.BlocksCount,
                    Eip7702DelegationCount = row.Eip7702DelegationCount,
                    LogsCount = row.LogsCount,
                    Owner = _addressCache.GetInfo((int)row.OwnerId),
                    Eip7702Delegate = _addressCache.GetInfo((int?)row.Eip7702DelegateId),
                },
                Data.Models.AddressType.XEvmContract => new XEvmContract
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.TezosX,
                    Runtime = Runtimes.Evm,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    AliasesCount = row.AliasesCount,
                    DepositOpsCount = row.DepositOpsCount,
                    ActiveBridgeTicketsCount = row.ActiveBridgeTicketsCount,
                    BridgeTicketBalancesCount = row.BridgeTicketBalancesCount,
                    BridgeTicketTransfersCount = row.BridgeTicketTransfersCount,
                    Counter = row.Counter,
                    Balance = (BigInteger)row.Balance18,
                    BlocksCount = row.BlocksCount,
                    Eip7702DelegationCount = row.Eip7702DelegationCount,
                    TypeHash = row.TypeHash,
                    CodeHash = row.CodeHash,
                    Creator = _addressCache.GetInfo((int)row.CreatorId),
                    LogsCount = row.LogsCount,
                    TokensCount = row.TokensCount,
                    Kind = ContractKinds.ToString((int)row.Kind),
                    Tags = ContractTags.ToList((int)row.Tags),
                },
                Data.Models.AddressType.XMichelsonUser => new XMichelsonUser
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.TezosX,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    AliasesCount = row.AliasesCount,
                    DepositOpsCount = row.DepositOpsCount,
                    Balance = row.Balance,
                    Index = row.Index,
                    RevealsCount = row.RevealsCount,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    RegisterConstantsCount = row.RegisterConstantsCount,
                    Counter = row.Counter,
                    Revealed = row.Revealed,
                    PublicKey = row.PublicKey,
                },
                Data.Models.AddressType.XMichelsonAlias => new XMichelsonAlias
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.TezosX,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    AliasesCount = row.AliasesCount,
                    DepositOpsCount = row.DepositOpsCount,
                    Balance = row.Balance,
                    Index = row.Index,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    Owner = _addressCache.GetInfo((int)row.OwnerId),
                },
                Data.Models.AddressType.XMichelsonContract => new XMichelsonContract
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.TezosX,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    AliasesCount = row.AliasesCount,
                    DepositOpsCount = row.DepositOpsCount,
                    Balance = row.Balance,
                    Index = row.Index,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                    Kind = ContractKinds.ToString((int)row.Kind),
                    TypeHash = row.TypeHash,
                    CodeHash = row.CodeHash,
                    Tags = ContractTags.ToList((int)row.Tags),
                    TokensCount = row.TokensCount,
                    LogsCount = row.LogsCount,
                    TicketsCount = row.TicketsCount,
                    Creator = _addressCache.GetInfo((int)row.CreatorId),
                },
                Data.Models.AddressType.XMichelsonGhost => new XMichelsonGhost
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Layer = Layers.TezosX,
                    Runtime = Runtimes.Michelson,
                    FirstLevel = row.FirstLevel,
                    FirstTimestamp = row.FirstTimestamp,
                    LastLevel = row.LastLevel,
                    LastTimestamp = row.LastTimestamp,
                    ContractsCount = row.ContractsCount,
                    ActiveTokensCount = row.ActiveTokensCount,
                    TokenBalancesCount = row.TokenBalancesCount,
                    TokenTransfersCount = row.TokenTransfersCount,
                    ActiveTicketsCount = row.ActiveTicketsCount,
                    TicketBalancesCount = row.TicketBalancesCount,
                    TicketTransfersCount = row.TicketTransfersCount,
                    TransactionsCount = row.TransactionsCount,
                    OriginationsCount = row.OriginationsCount,
                    MigrationsCount = row.MigrationsCount,
                    AliasesCount = row.AliasesCount,
                    DepositOpsCount = row.DepositOpsCount,
                    Balance = row.Balance,
                    Index = row.Index,
                    TransferTicketCount = row.TransferTicketCount,
                    IncreasePaidStorageCount = row.IncreasePaidStorageCount,
                },
                _ => throw new InvalidOperationException("Failed to read Address")
            };
        });
    }

    public async Task<object?[][]> Get(AddressFilter filter, Pagination pagination, Selection selection)
    {
        var rows = await Query(filter, pagination, selection);

        var fields = selection.Fields();
        var result = new object?[rows.Count()][];
        for (int i = 0; i < result.Length; i++)
            result[i] = new object?[fields.Count];

        for (int i = 0, j = 0; i < fields.Count; j = 0, i++)
        {
            switch (fields[i].Full)
            {
                case "type":
                    foreach (var row in rows) result[j++][i] = AddressTypes.ToString((int)row.Type);
                    break;
                case "id":
                    foreach (var row in rows) result[j++][i] = row.Id;
                    break;
                case "chain":
                    foreach (var row in rows) result[j++][i] = _chainCache.GetInfo((int)row.ChainId);
                    break;
                case "chain.id":
                    foreach (var row in rows) result[j++][i] = row.ChainId;
                    break;
                case "chain.chainId":
                    foreach (var row in rows) result[j++][i] = _chainCache.GetInfo((int)row.ChainId).ChainId;
                    break;
                case "chain.layer":
                    foreach (var row in rows) result[j++][i] = _chainCache.GetInfo((int)row.ChainId).Layer;
                    break;
                case "hash":
                    foreach (var row in rows) result[j++][i] = row.Hash;
                    break;
                case "layer":
                    foreach (var row in rows) result[j++][i] = Layers.ToString((int)row.Layer);
                    break;
                case "runtime":
                    foreach (var row in rows) result[j++][i] = Runtimes.ToString((int)row.Runtime);
                    break;
                case "firstLevel":
                    foreach (var row in rows) result[j++][i] = row.FirstLevel;
                    break;
                case "firstTimestamp":
                    foreach (var row in rows) result[j++][i] = row.FirstTimestamp;
                    break;
                case "lastLevel":
                    foreach (var row in rows) result[j++][i] = row.LastLevel;
                    break;
                case "lastTimestamp":
                    foreach (var row in rows) result[j++][i] = row.LastTimestamp;
                    break;
                case "contractsCount":
                    foreach (var row in rows) result[j++][i] = row.ContractsCount;
                    break;
                case "activeTokensCount":
                    foreach (var row in rows) result[j++][i] = row.ActiveTokensCount;
                    break;
                case "tokenBalancesCount":
                    foreach (var row in rows) result[j++][i] = row.TokenBalancesCount;
                    break;
                case "tokenTransfersCount":
                    foreach (var row in rows) result[j++][i] = row.TokenTransfersCount;
                    break;
                case "activeTicketsCount":
                    foreach (var row in rows) result[j++][i] = row.ActiveTicketsCount;
                    break;
                case "ticketBalancesCount":
                    foreach (var row in rows) result[j++][i] = row.TicketBalancesCount;
                    break;
                case "ticketTransfersCount":
                    foreach (var row in rows) result[j++][i] = row.TicketTransfersCount;
                    break;
                case "transactionsCount":
                    foreach (var row in rows) result[j++][i] = row.TransactionsCount;
                    break;
                case "originationsCount":
                    foreach (var row in rows) result[j++][i] = row.OriginationsCount;
                    break;
                case "migrationsCount":
                    foreach (var row in rows) result[j++][i] = row.MigrationsCount;
                    break;
                case "balance":
                    foreach (var row in rows) result[j++][i] = ((object?)row.Balance18) ?? (object?)row.Balance;
                    break;
                case "aliasesCount":
                    foreach (var row in rows) result[j++][i] = row.AliasesCount;
                    break;
                case "depositOpsCount":
                    foreach (var row in rows) result[j++][i] = row.DepositOpsCount;
                    break;
                case "activeBridgeTicketsCount":
                    foreach (var row in rows) result[j++][i] = row.ActiveBridgeTicketsCount;
                    break;
                case "bridgeTicketBalancesCount":
                    foreach (var row in rows) result[j++][i] = row.BridgeTicketBalancesCount;
                    break;
                case "bridgeTicketTransfersCount":
                    foreach (var row in rows) result[j++][i] = row.BridgeTicketTransfersCount;
                    break;
                case "smartRollupBonds":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupBonds;
                    break;
                case "counter":
                    foreach (var row in rows) result[j++][i] = row.Counter;
                    break;
                case "baker":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.BakerId);
                    break;
                case "baker.id":
                    foreach (var row in rows) result[j++][i] = row.BakerId;
                    break;
                case "baker.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.BakerId))?.Hash;
                    break;
                case "baker.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.BakerId))?.Type;
                    break;
                case "baker.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.BakerId))?.Alias;
                    break;
                case "delegationLevel":
                    foreach (var row in rows) result[j++][i] = row.DelegationLevel;
                    break;
                case "delegationTimestamp":
                    foreach (var row in rows) result[j++][i] = row.DelegationTimestamp;
                    break;
                case "staked":
                    foreach (var row in rows) result[j++][i] = row.Staked;
                    break;
                case "index":
                    foreach (var row in rows) result[j++][i] = row.Index;
                    break;
                case "smartRollupsCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupsCount;
                    break;
                case "delegationsCount":
                    foreach (var row in rows) result[j++][i] = row.DelegationsCount;
                    break;
                case "revealsCount":
                    foreach (var row in rows) result[j++][i] = row.RevealsCount;
                    break;
                case "transferTicketCount":
                    foreach (var row in rows) result[j++][i] = row.TransferTicketCount;
                    break;
                case "increasePaidStorageCount":
                    foreach (var row in rows) result[j++][i] = row.IncreasePaidStorageCount;
                    break;
                case "updateSecondaryKeyCount":
                    foreach (var row in rows) result[j++][i] = row.UpdateSecondaryKeyCount;
                    break;
                case "drainDelegateCount":
                    foreach (var row in rows) result[j++][i] = row.DrainDelegateCount;
                    break;
                case "subsidyCount":
                    foreach (var row in rows) result[j++][i] = row.SubsidyCount;
                    break;
                case "smartRollupAddMessagesCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupAddMessagesCount;
                    break;
                case "smartRollupCementCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupCementCount;
                    break;
                case "smartRollupExecuteCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupExecuteCount;
                    break;
                case "smartRollupOriginateCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupOriginateCount;
                    break;
                case "smartRollupPublishCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupPublishCount;
                    break;
                case "smartRollupRecoverBondCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupRecoverBondCount;
                    break;
                case "smartRollupRefuteCount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupRefuteCount;
                    break;
                case "refutationGamesCount":
                    foreach (var row in rows) result[j++][i] = row.RefutationGamesCount;
                    break;
                case "activeRefutationGamesCount":
                    foreach (var row in rows) result[j++][i] = row.ActiveRefutationGamesCount;
                    break;
                case "revealed":
                    foreach (var row in rows) result[j++][i] = row.Revealed;
                    break;
                case "publicKey":
                    foreach (var row in rows) result[j++][i] = row.PublicKey;
                    break;
                case "registerConstantsCount":
                    foreach (var row in rows) result[j++][i] = row.RegisterConstantsCount;
                    break;
                case "stakedPseudotokens":
                    foreach (var row in rows) result[j++][i] = (object?)row.StakedPseudotokens;
                    break;
                case "unstakedBalance":
                    foreach (var row in rows) result[j++][i] = row.UnstakedBalance;
                    break;
                case "unstakedBaker":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.UnstakedBakerId);
                    break;
                case "unstakedBaker.id":
                    foreach (var row in rows) result[j++][i] = row.UnstakedBakerId;
                    break;
                case "unstakedBaker.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.UnstakedBakerId))?.Hash;
                    break;
                case "unstakedBaker.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.UnstakedBakerId))?.Type;
                    break;
                case "unstakedBaker.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.UnstakedBakerId))?.Alias;
                    break;
                case "stakingUpdatesCount":
                    foreach (var row in rows) result[j++][i] = row.StakingUpdatesCount;
                    break;
                case "activationsCount":
                    foreach (var row in rows) result[j++][i] = row.ActivationsCount;
                    break;
                case "setDepositsLimitsCount":
                    foreach (var row in rows) result[j++][i] = row.SetDepositsLimitsCount;
                    break;
                case "stakingOpsCount":
                    foreach (var row in rows) result[j++][i] = row.StakingOpsCount;
                    break;
                case "setDelegateParametersOpsCount":
                    foreach (var row in rows) result[j++][i] = row.SetDelegateParametersOpsCount;
                    break;
                case "dalPublishCommitmentOpsCount":
                    foreach (var row in rows) result[j++][i] = row.DalPublishCommitmentOpsCount;
                    break;
                case "activationLevel":
                    foreach (var row in rows) result[j++][i] = row.ActivationLevel;
                    break;
                case "activationTimestamp":
                    foreach (var row in rows) result[j++][i] = row.ActivationTimestamp;
                    break;
                case "deactivationLevel":
                    foreach (var row in rows) result[j++][i] = row.DeactivationLevel;
                    break;
                case "consensusAddress":
                    foreach (var row in rows) result[j++][i] = row.ConsensusAddress;
                    break;
                case "companionAddress":
                    foreach (var row in rows) result[j++][i] = row.CompanionAddress;
                    break;
                case "bakingPower":
                    foreach (var row in rows) result[j++][i] = row.BakingPower;
                    break;
                case "votingPower":
                    foreach (var row in rows) result[j++][i] = row.VotingPower;
                    break;
                case "ownDelegatedBalance":
                    foreach (var row in rows) result[j++][i] = row.OwnDelegatedBalance;
                    break;
                case "externalDelegatedBalance":
                    foreach (var row in rows) result[j++][i] = row.ExternalDelegatedBalance;
                    break;
                case "minTotalDelegated":
                    foreach (var row in rows) result[j++][i] = row.MinTotalDelegated;
                    break;
                case "minTotalDelegatedLevel":
                    foreach (var row in rows) result[j++][i] = row.MinTotalDelegatedLevel;
                    break;
                case "delegatorsCount":
                    foreach (var row in rows) result[j++][i] = row.DelegatorsCount;
                    break;
                case "ownStakedBalance":
                    foreach (var row in rows) result[j++][i] = row.OwnStakedBalance;
                    break;
                case "externalStakedBalance":
                    foreach (var row in rows) result[j++][i] = row.ExternalStakedBalance;
                    break;
                case "issuedPseudotokens":
                    foreach (var row in rows) result[j++][i] = (object?)row.IssuedPseudotokens;
                    break;
                case "stakersCount":
                    foreach (var row in rows) result[j++][i] = row.StakersCount;
                    break;
                case "externalUnstakedBalance":
                    foreach (var row in rows) result[j++][i] = row.ExternalUnstakedBalance;
                    break;
                case "roundingError":
                    foreach (var row in rows) result[j++][i] = row.RoundingError;
                    break;
                case "frozenDepositLimit":
                    foreach (var row in rows) result[j++][i] = row.FrozenDepositLimit;
                    break;
                case "limitOfStakingOverBaking":
                    foreach (var row in rows) result[j++][i] = row.LimitOfStakingOverBaking;
                    break;
                case "edgeOfBakingOverStaking":
                    foreach (var row in rows) result[j++][i] = row.EdgeOfBakingOverStaking;
                    break;
                case "blocksCount":
                    foreach (var row in rows) result[j++][i] = row.BlocksCount;
                    break;
                case "attestationsCount":
                    foreach (var row in rows) result[j++][i] = row.AttestationsCount;
                    break;
                case "preattestationsCount":
                    foreach (var row in rows) result[j++][i] = row.PreattestationsCount;
                    break;
                case "ballotsCount":
                    foreach (var row in rows) result[j++][i] = row.BallotsCount;
                    break;
                case "proposalsCount":
                    foreach (var row in rows) result[j++][i] = row.ProposalsCount;
                    break;
                case "dalEntrapmentEvidenceOpsCount":
                    foreach (var row in rows) result[j++][i] = row.DalEntrapmentEvidenceOpsCount;
                    break;
                case "doubleBakingCount":
                    foreach (var row in rows) result[j++][i] = row.DoubleBakingCount;
                    break;
                case "doubleConsensusCount":
                    foreach (var row in rows) result[j++][i] = row.DoubleConsensusCount;
                    break;
                case "nonceRevelationsCount":
                    foreach (var row in rows) result[j++][i] = row.NonceRevelationsCount;
                    break;
                case "vdfRevelationsCount":
                    foreach (var row in rows) result[j++][i] = row.VdfRevelationsCount;
                    break;
                case "revelationPenaltiesCount":
                    foreach (var row in rows) result[j++][i] = row.RevelationPenaltiesCount;
                    break;
                case "attestationRewardsCount":
                    foreach (var row in rows) result[j++][i] = row.AttestationRewardsCount;
                    break;
                case "dalAttestationRewardsCount":
                    foreach (var row in rows) result[j++][i] = row.DalAttestationRewardsCount;
                    break;
                case "autostakingOpsCount":
                    foreach (var row in rows) result[j++][i] = row.AutostakingOpsCount;
                    break;
                case "software":
                    foreach (var row in rows) result[j++][i] = _softwareCache.GetInfo((int?)row.SoftwareId);
                    break;
                case "software.id":
                    foreach (var row in rows) result[j++][i] = row.SoftwareId;
                    break;
                case "software.shortHash":
                    foreach (var row in rows) result[j++][i] = _softwareCache.GetInfo((int?)row.SoftwareId)?.ShortHash;
                    break;
                case "softwareUpdateLevel":
                    foreach (var row in rows) result[j++][i] = row.SoftwareUpdateLevel;
                    break;
                case "kind":
                    foreach (var row in rows) result[j++][i] = row.Kind == null ? null : ContractKinds.ToString((int)row.Kind);
                    break;
                case "typeHash":
                    foreach (var row in rows) result[j++][i] = row.TypeHash;
                    break;
                case "codeHash":
                    foreach (var row in rows) result[j++][i] = row.CodeHash;
                    break;
                case "creator":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.CreatorId);
                    break;
                case "creator.id":
                    foreach (var row in rows) result[j++][i] = row.CreatorId;
                    break;
                case "creator.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.CreatorId))?.Hash;
                    break;
                case "creator.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.CreatorId))?.Type;
                    break;
                case "creator.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.CreatorId))?.Alias;
                    break;
                case "logsCount":
                    foreach (var row in rows) result[j++][i] = row.LogsCount;
                    break;
                case "tokensCount":
                    foreach (var row in rows) result[j++][i] = row.TokensCount;
                    break;
                case "tags":
                    foreach (var row in rows) result[j++][i] = row.Tags == null ? null : ContractTags.ToList((int)row.Tags);
                    break;
                case "ticketsCount":
                    foreach (var row in rows) result[j++][i] = row.TicketsCount;
                    break;
                case "pvmKind":
                    foreach (var row in rows) result[j++][i] = row.PvmKind == null ? null : PvmKinds.ToString((int)row.PvmKind);
                    break;
                case "parameterSchema":
                    foreach (var row in rows) result[j++][i] = row.ParameterSchema;
                    break;
                case "genesisCommitment":
                    foreach (var row in rows) result[j++][i] = row.GenesisCommitment;
                    break;
                case "lastCommitment":
                    foreach (var row in rows) result[j++][i] = row.LastCommitment;
                    break;
                case "inboxLevel":
                    foreach (var row in rows) result[j++][i] = row.InboxLevel;
                    break;
                case "totalStakers":
                    foreach (var row in rows) result[j++][i] = row.TotalStakers;
                    break;
                case "activeStakers":
                    foreach (var row in rows) result[j++][i] = row.ActiveStakers;
                    break;
                case "executedCommitments":
                    foreach (var row in rows) result[j++][i] = row.ExecutedCommitments;
                    break;
                case "cementedCommitments":
                    foreach (var row in rows) result[j++][i] = row.CementedCommitments;
                    break;
                case "pendingCommitments":
                    foreach (var row in rows) result[j++][i] = row.PendingCommitments;
                    break;
                case "refutedCommitments":
                    foreach (var row in rows) result[j++][i] = row.RefutedCommitments;
                    break;
                case "orphanCommitments":
                    foreach (var row in rows) result[j++][i] = row.OrphanCommitments;
                    break;
                case "eip7702DelegationCount":
                    foreach (var row in rows) result[j++][i] = row.Eip7702DelegationCount;
                    break;
                case "eip7702Delegate":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.Eip7702DelegateId);
                    break;
                case "eip7702Delegate.id":
                    foreach (var row in rows) result[j++][i] = row.Eip7702DelegateId;
                    break;
                case "eip7702Delegate.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Eip7702DelegateId))?.Hash;
                    break;
                case "eip7702Delegate.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Eip7702DelegateId))?.Type;
                    break;
                case "eip7702Delegate.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Eip7702DelegateId))?.Alias;
                    break;
                case "owner":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.OwnerId);
                    break;
                case "owner.id":
                    foreach (var row in rows) result[j++][i] = row.OwnerId;
                    break;
                case "owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.OwnerId))?.Hash;
                    break;
                case "owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.OwnerId))?.Type;
                    break;
                case "owner.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.OwnerId))?.Alias;
                    break;
            }
        }

        return result;
    }
}
