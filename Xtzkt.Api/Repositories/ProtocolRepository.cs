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

public class ProtocolRepository(ChainCache _chainCache, NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",         (@"""Id""",         "integer") },
        { "firstLevel", (@"""FirstLevel""", "integer") },
    };

    void ProcessFilters(ProtocolFilter filter)
    {
        filter.Chain?.Id += filter.Chain.ChainId?.ToIdParameter(_chainCache);
    }

    async Task<IEnumerable<dynamic>> Query(ProtocolFilter filter, Pagination pagination, Selection? selection = null)
    {
        ProcessFilters(filter);

        var columns = new HashSet<string>();
        if (selection != null)
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "layer":                              columns.Add(@"""Layer"""); break;
                    // Protocol
                    case "id":                                 columns.Add(@"""Id"""); break;
                    case "chain":                              columns.Add(@"""ChainId"""); break;
                    case "hash":                               columns.Add(@"""Hash"""); break;
                    case "version":                            columns.Add(@"""Version"""); break;
                    case "firstLevel":                         columns.Add(@"""FirstLevel"""); break;
                    case "lastLevel":                          columns.Add(@"""LastLevel"""); break;
                    // L1Protocol
                    case "firstCycle":                         columns.Add(@"""FirstCycle"""); break;
                    case "firstCycleLevel":                    columns.Add(@"""FirstCycleLevel"""); break;
                    case "rampUpCycles":                       columns.Add(@"""RampUpCycles"""); break;
                    case "noRewardCycles":                     columns.Add(@"""NoRewardCycles"""); break;
                    case "consensusRightsDelay":               columns.Add(@"""ConsensusRightsDelay"""); break;
                    case "bakerParametersActivationDelay":     columns.Add(@"""BakerParametersActivationDelay"""); break;
                    case "blocksPerCycle":                     columns.Add(@"""BlocksPerCycle"""); break;
                    case "blocksPerCommitment":                columns.Add(@"""BlocksPerCommitment"""); break;
                    case "blocksPerSnapshot":                  columns.Add(@"""BlocksPerSnapshot"""); break;
                    case "blocksPerVoting":                    columns.Add(@"""BlocksPerVoting"""); break;
                    case "timeBetweenBlocks":                  columns.Add(@"""TimeBetweenBlocks"""); break;
                    case "attestersPerBlock":                  columns.Add(@"""AttestersPerBlock"""); break;
                    case "hardOperationGasLimit":              columns.Add(@"""HardOperationGasLimit"""); break;
                    case "hardOperationStorageLimit":          columns.Add(@"""HardOperationStorageLimit"""); break;
                    case "hardBlockGasLimit":                  columns.Add(@"""HardBlockGasLimit"""); break;
                    case "minimalStake":                       columns.Add(@"""MinimalStake"""); break;
                    case "minimalFrozenStake":                 columns.Add(@"""MinimalFrozenStake"""); break;
                    case "blockDeposit":                       columns.Add(@"""BlockDeposit"""); break;
                    case "blockReward0":                       columns.Add(@"""BlockReward0"""); break;
                    case "blockReward1":                       columns.Add(@"""BlockReward1"""); break;
                    case "maxBakingReward":                    columns.Add(@"""MaxBakingReward"""); break;
                    case "attestationDeposit":                 columns.Add(@"""AttestationDeposit"""); break;
                    case "attestationReward0":                 columns.Add(@"""AttestationReward0"""); break;
                    case "attestationReward1":                 columns.Add(@"""AttestationReward1"""); break;
                    case "maxAttestationReward":               columns.Add(@"""MaxAttestationReward"""); break;
                    case "originationSize":                    columns.Add(@"""OriginationSize"""); break;
                    case "byteCost":                           columns.Add(@"""ByteCost"""); break;
                    case "proposalQuorum":                     columns.Add(@"""ProposalQuorum"""); break;
                    case "ballotQuorumMin":                    columns.Add(@"""BallotQuorumMin"""); break;
                    case "ballotQuorumMax":                    columns.Add(@"""BallotQuorumMax"""); break;
                    case "lBToggleThreshold":                  columns.Add(@"""LBToggleThreshold"""); break;
                    case "consensusThreshold":                 columns.Add(@"""ConsensusThreshold"""); break;
                    case "minParticipationNumerator":          columns.Add(@"""MinParticipationNumerator"""); break;
                    case "minParticipationDenominator":        columns.Add(@"""MinParticipationDenominator"""); break;
                    case "denunciationPeriod":                 columns.Add(@"""DenunciationPeriod"""); break;
                    case "slashingDelay":                      columns.Add(@"""SlashingDelay"""); break;
                    case "maxDelegatedOverFrozenRatio":        columns.Add(@"""MaxDelegatedOverFrozenRatio"""); break;
                    case "maxExternalOverOwnStakeRatio":       columns.Add(@"""MaxExternalOverOwnStakeRatio"""); break;
                    case "stakePowerMultiplier":               columns.Add(@"""StakePowerMultiplier"""); break;
                    case "smartRollupOriginationSize":         columns.Add(@"""SmartRollupOriginationSize"""); break;
                    case "smartRollupStakeAmount":             columns.Add(@"""SmartRollupStakeAmount"""); break;
                    case "smartRollupChallengeWindow":         columns.Add(@"""SmartRollupChallengeWindow"""); break;
                    case "smartRollupCommitmentPeriod":        columns.Add(@"""SmartRollupCommitmentPeriod"""); break;
                    case "smartRollupTimeoutPeriod":           columns.Add(@"""SmartRollupTimeoutPeriod"""); break;
                    case "dictator":                           columns.Add(@"""Dictator"""); break;
                    case "doubleBakingSlashedPercentage":      columns.Add(@"""DoubleBakingSlashedPercentage"""); break;
                    case "doubleConsensusSlashedPercentage":   columns.Add(@"""DoubleConsensusSlashedPercentage"""); break;
                    case "numberOfShards":                     columns.Add(@"""NumberOfShards"""); break;
                    case "toleratedInactivityPeriod":          columns.Add(@"""ToleratedInactivityPeriod"""); break;
                    // XProtocol
                    case "michelsonHash":                      columns.Add(@"""MichelsonHash"""); break;
                    case "minBlockTimeMs":                     columns.Add(@"""MinBlockTimeMs"""); break;
                    case "maxBlockTimeMs":                     columns.Add(@"""MaxBlockTimeMs"""); break;
                    case "daFeePerByte":                       columns.Add(@"""DaFeePerByte"""); break;
                    case "daFeePerByte18":                     columns.Add(@"""DaFeePerByte18"""); break;
                    case "hardEvmBlockGasLimit":               columns.Add(@"""HardEvmBlockGasLimit"""); break;
                    case "hardEvmOperationGasLimit":           columns.Add(@"""HardEvmOperationGasLimit"""); break;
                    case "hardMichelsonBlockGasLimit":         columns.Add(@"""HardBlockGasLimit"""); break;
                    case "hardMichelsonOperationGasLimit":     columns.Add(@"""HardOperationGasLimit"""); break;
                    case "hardMichelsonOperationStorageLimit": columns.Add(@"""HardOperationStorageLimit"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Protocols""")
            .Where(@"""Id""",         filter.Id)
            .Where(@"""ChainId""",    filter.Chain?.Id)
            .Where(@"""Hash""",       filter.Hash)
            .Where(@"""FirstLevel""", filter.FirstLevel)
            .Where(@"""LastLevel""",  filter.LastLevel)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(ProtocolFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.ProtocolsCount);

        ProcessFilters(filter);

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Protocols""")
            .Where(@"""Id""",         filter.Id)
            .Where(@"""ChainId""",    filter.Chain?.Id)
            .Where(@"""Hash""",       filter.Hash)
            .Where(@"""FirstLevel""", filter.FirstLevel)
            .Where(@"""LastLevel""",  filter.LastLevel)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Protocol>> Get(ProtocolFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, Protocol>(row =>
        {
            if (row.Layer == (int)Data.Models.Layer.L1)
                return new L1Protocol
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Version = row.Version,
                    FirstLevel = row.FirstLevel,
                    LastLevel = row.LastLevel,
                    FirstCycle = row.FirstCycle,
                    FirstCycleLevel = row.FirstCycleLevel,
                    RampUpCycles = row.RampUpCycles,
                    NoRewardCycles = row.NoRewardCycles,
                    ConsensusRightsDelay = row.ConsensusRightsDelay,
                    BakerParametersActivationDelay = row.BakerParametersActivationDelay,
                    BlocksPerCycle = row.BlocksPerCycle,
                    BlocksPerCommitment = row.BlocksPerCommitment,
                    BlocksPerSnapshot = row.BlocksPerSnapshot,
                    BlocksPerVoting = row.BlocksPerVoting,
                    TimeBetweenBlocks = row.TimeBetweenBlocks,
                    AttestersPerBlock = row.AttestersPerBlock,
                    HardOperationGasLimit = row.HardOperationGasLimit,
                    HardOperationStorageLimit = row.HardOperationStorageLimit,
                    HardBlockGasLimit = row.HardBlockGasLimit,
                    MinimalStake = row.MinimalStake,
                    MinimalFrozenStake = row.MinimalFrozenStake,
                    BlockDeposit = row.BlockDeposit,
                    BlockReward0 = row.BlockReward0,
                    BlockReward1 = row.BlockReward1,
                    MaxBakingReward = row.MaxBakingReward,
                    AttestationDeposit = row.AttestationDeposit,
                    AttestationReward0 = row.AttestationReward0,
                    AttestationReward1 = row.AttestationReward1,
                    MaxAttestationReward = row.MaxAttestationReward,
                    OriginationSize = row.OriginationSize,
                    ByteCost = row.ByteCost,
                    ProposalQuorum = row.ProposalQuorum,
                    BallotQuorumMin = row.BallotQuorumMin,
                    BallotQuorumMax = row.BallotQuorumMax,
                    LBToggleThreshold = row.LBToggleThreshold,
                    ConsensusThreshold = row.ConsensusThreshold,
                    MinParticipationNumerator = row.MinParticipationNumerator,
                    MinParticipationDenominator = row.MinParticipationDenominator,
                    DenunciationPeriod = row.DenunciationPeriod,
                    SlashingDelay = row.SlashingDelay,
                    MaxDelegatedOverFrozenRatio = row.MaxDelegatedOverFrozenRatio,
                    MaxExternalOverOwnStakeRatio = row.MaxExternalOverOwnStakeRatio,
                    StakePowerMultiplier = row.StakePowerMultiplier,
                    SmartRollupOriginationSize = row.SmartRollupOriginationSize,
                    SmartRollupStakeAmount = row.SmartRollupStakeAmount,
                    SmartRollupChallengeWindow = row.SmartRollupChallengeWindow,
                    SmartRollupCommitmentPeriod = row.SmartRollupCommitmentPeriod,
                    SmartRollupTimeoutPeriod = row.SmartRollupTimeoutPeriod,
                    Dictator = row.Dictator,
                    DoubleBakingSlashedPercentage = row.DoubleBakingSlashedPercentage,
                    DoubleConsensusSlashedPercentage = row.DoubleConsensusSlashedPercentage,
                    NumberOfShards = row.NumberOfShards,
                    ToleratedInactivityPeriod = row.ToleratedInactivityPeriod,
                };

            if (row.Layer == (int)Data.Models.Layer.TezosX)
                return new XProtocol
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Hash = row.Hash,
                    Version = row.Version,
                    FirstLevel = row.FirstLevel,
                    LastLevel = row.LastLevel,
                    MichelsonHash = row.MichelsonHash,
                    MinBlockTimeMs = row.MinBlockTimeMs,
                    MaxBlockTimeMs = row.MaxBlockTimeMs,
                    OriginationSize = row.OriginationSize,
                    ByteCost = row.ByteCost,
                    DaFeePerByte = row.DaFeePerByte,
                    DaFeePerByte18 = (BigInteger)row.DaFeePerByte18,
                    HardEvmBlockGasLimit = row.HardEvmBlockGasLimit,
                    HardEvmOperationGasLimit = row.HardEvmOperationGasLimit,
                    HardMichelsonBlockGasLimit = row.HardBlockGasLimit,
                    HardMichelsonOperationGasLimit = row.HardOperationGasLimit,
                    HardMichelsonOperationStorageLimit = row.HardOperationStorageLimit,
                };

            throw new InvalidOperationException("Failed to read Protocol");
        });
    }

    public async Task<object?[][]> Get(ProtocolFilter filter, Pagination pagination, Selection selection)
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
                case "layer":
                    foreach (var row in rows) result[j++][i] = Layers.ToString(row.Layer);
                    break;
                // Protocol
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
                case "version":
                    foreach (var row in rows) result[j++][i] = row.Version;
                    break;
                case "firstLevel":
                    foreach (var row in rows) result[j++][i] = row.FirstLevel;
                    break;
                case "lastLevel":
                    foreach (var row in rows) result[j++][i] = row.LastLevel;
                    break;
                // L1Protocol
                case "firstCycle":
                    foreach (var row in rows) result[j++][i] = row.FirstCycle;
                    break;
                case "firstCycleLevel":
                    foreach (var row in rows) result[j++][i] = row.FirstCycleLevel;
                    break;
                case "rampUpCycles":
                    foreach (var row in rows) result[j++][i] = row.RampUpCycles;
                    break;
                case "noRewardCycles":
                    foreach (var row in rows) result[j++][i] = row.NoRewardCycles;
                    break;
                case "consensusRightsDelay":
                    foreach (var row in rows) result[j++][i] = row.ConsensusRightsDelay;
                    break;
                case "bakerParametersActivationDelay":
                    foreach (var row in rows) result[j++][i] = row.BakerParametersActivationDelay;
                    break;
                case "blocksPerCycle":
                    foreach (var row in rows) result[j++][i] = row.BlocksPerCycle;
                    break;
                case "blocksPerCommitment":
                    foreach (var row in rows) result[j++][i] = row.BlocksPerCommitment;
                    break;
                case "blocksPerSnapshot":
                    foreach (var row in rows) result[j++][i] = row.BlocksPerSnapshot;
                    break;
                case "blocksPerVoting":
                    foreach (var row in rows) result[j++][i] = row.BlocksPerVoting;
                    break;
                case "timeBetweenBlocks":
                    foreach (var row in rows) result[j++][i] = row.TimeBetweenBlocks;
                    break;
                case "attestersPerBlock":
                    foreach (var row in rows) result[j++][i] = row.AttestersPerBlock;
                    break;
                case "hardOperationGasLimit":
                    foreach (var row in rows) result[j++][i] = row.HardOperationGasLimit;
                    break;
                case "hardOperationStorageLimit":
                    foreach (var row in rows) result[j++][i] = row.HardOperationStorageLimit;
                    break;
                case "hardBlockGasLimit":
                    foreach (var row in rows) result[j++][i] = row.HardBlockGasLimit;
                    break;
                case "minimalStake":
                    foreach (var row in rows) result[j++][i] = row.MinimalStake;
                    break;
                case "minimalFrozenStake":
                    foreach (var row in rows) result[j++][i] = row.MinimalFrozenStake;
                    break;
                case "blockDeposit":
                    foreach (var row in rows) result[j++][i] = row.BlockDeposit;
                    break;
                case "blockReward0":
                    foreach (var row in rows) result[j++][i] = row.BlockReward0;
                    break;
                case "blockReward1":
                    foreach (var row in rows) result[j++][i] = row.BlockReward1;
                    break;
                case "maxBakingReward":
                    foreach (var row in rows) result[j++][i] = row.MaxBakingReward;
                    break;
                case "attestationDeposit":
                    foreach (var row in rows) result[j++][i] = row.AttestationDeposit;
                    break;
                case "attestationReward0":
                    foreach (var row in rows) result[j++][i] = row.AttestationReward0;
                    break;
                case "attestationReward1":
                    foreach (var row in rows) result[j++][i] = row.AttestationReward1;
                    break;
                case "maxAttestationReward":
                    foreach (var row in rows) result[j++][i] = row.MaxAttestationReward;
                    break;
                case "originationSize":
                    foreach (var row in rows) result[j++][i] = row.OriginationSize;
                    break;
                case "byteCost":
                    foreach (var row in rows) result[j++][i] = row.ByteCost;
                    break;
                case "proposalQuorum":
                    foreach (var row in rows) result[j++][i] = row.ProposalQuorum;
                    break;
                case "ballotQuorumMin":
                    foreach (var row in rows) result[j++][i] = row.BallotQuorumMin;
                    break;
                case "ballotQuorumMax":
                    foreach (var row in rows) result[j++][i] = row.BallotQuorumMax;
                    break;
                case "lBToggleThreshold":
                    foreach (var row in rows) result[j++][i] = row.LBToggleThreshold;
                    break;
                case "consensusThreshold":
                    foreach (var row in rows) result[j++][i] = row.ConsensusThreshold;
                    break;
                case "minParticipationNumerator":
                    foreach (var row in rows) result[j++][i] = row.MinParticipationNumerator;
                    break;
                case "minParticipationDenominator":
                    foreach (var row in rows) result[j++][i] = row.MinParticipationDenominator;
                    break;
                case "denunciationPeriod":
                    foreach (var row in rows) result[j++][i] = row.DenunciationPeriod;
                    break;
                case "slashingDelay":
                    foreach (var row in rows) result[j++][i] = row.SlashingDelay;
                    break;
                case "maxDelegatedOverFrozenRatio":
                    foreach (var row in rows) result[j++][i] = row.MaxDelegatedOverFrozenRatio;
                    break;
                case "maxExternalOverOwnStakeRatio":
                    foreach (var row in rows) result[j++][i] = row.MaxExternalOverOwnStakeRatio;
                    break;
                case "stakePowerMultiplier":
                    foreach (var row in rows) result[j++][i] = row.StakePowerMultiplier;
                    break;
                case "smartRollupOriginationSize":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupOriginationSize;
                    break;
                case "smartRollupStakeAmount":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupStakeAmount;
                    break;
                case "smartRollupChallengeWindow":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupChallengeWindow;
                    break;
                case "smartRollupCommitmentPeriod":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupCommitmentPeriod;
                    break;
                case "smartRollupTimeoutPeriod":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupTimeoutPeriod;
                    break;
                case "dictator":
                    foreach (var row in rows) result[j++][i] = row.Dictator;
                    break;
                case "doubleBakingSlashedPercentage":
                    foreach (var row in rows) result[j++][i] = row.DoubleBakingSlashedPercentage;
                    break;
                case "doubleConsensusSlashedPercentage":
                    foreach (var row in rows) result[j++][i] = row.DoubleConsensusSlashedPercentage;
                    break;
                case "numberOfShards":
                    foreach (var row in rows) result[j++][i] = row.NumberOfShards;
                    break;
                case "toleratedInactivityPeriod":
                    foreach (var row in rows) result[j++][i] = row.ToleratedInactivityPeriod;
                    break;
                // XProtocol
                case "michelsonHash":
                    foreach (var row in rows) result[j++][i] = row.MichelsonHash;
                    break;
                case "minBlockTimeMs":
                    foreach (var row in rows) result[j++][i] = row.MinBlockTimeMs;
                    break;
                case "maxBlockTimeMs":
                    foreach (var row in rows) result[j++][i] = row.MaxBlockTimeMs;
                    break;
                case "daFeePerByte":
                    foreach (var row in rows) result[j++][i] = row.DaFeePerByte;
                    break;
                case "daFeePerByte18":
                    foreach (var row in rows) result[j++][i] = row.DaFeePerByte18;
                    break;
                case "hardEvmBlockGasLimit":
                    foreach (var row in rows) result[j++][i] = row.HardEvmBlockGasLimit;
                    break;
                case "hardEvmOperationGasLimit":
                    foreach (var row in rows) result[j++][i] = row.HardEvmOperationGasLimit;
                    break;
                case "hardMichelsonBlockGasLimit":
                    foreach (var row in rows) result[j++][i] = row.HardBlockGasLimit;
                    break;
                case "hardMichelsonOperationGasLimit":
                    foreach (var row in rows) result[j++][i] = row.HardOperationGasLimit;
                    break;
                case "hardMichelsonOperationStorageLimit":
                    foreach (var row in rows) result[j++][i] = row.HardOperationStorageLimit;
                    break;
            }
        }

        return result;
    }
}