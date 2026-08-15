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

public class BlockRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    ProtocolCache _protocolCache,
    SoftwareCache _softwareCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"""Id""",        "bigint") },
        { "level",     (@"""Level""",     "integer") },
        { "timestamp", (@"""Timestamp""", "timestamptz") },
    };

    void ProcessFilters(BlockFilter filter)
    {
        filter.Chain?.Id += filter.Chain.ChainId?.ToIdParameter(_chainCache);
    }

    async Task<IEnumerable<dynamic>> Query(BlockFilter filter, Pagination pagination, Selection? selection = null)
    {
        ProcessFilters(filter);

        var columns = new HashSet<string>();
        if (selection != null)
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "layer":                columns.Add(@"""Layer"""); break;
                    // Block
                    case "id":                   columns.Add(@"""Id"""); break;
                    case "chain":                columns.Add(@"""ChainId"""); break;
                    case "level":                columns.Add(@"""Level"""); break;
                    case "hash":                 columns.Add(@"""Hash"""); break;
                    case "timestamp":            columns.Add(@"""Timestamp"""); break;
                    // L1Block
                    case "cycle":                columns.Add(@"""Cycle"""); break;
                    case "protocol":             columns.Add(@"""ProtocolId"""); break;
                    case "software":             columns.Add(@"""SoftwareId"""); break;
                    case "payloadRound":         columns.Add(@"""PayloadRound"""); break;
                    case "blockRound":           columns.Add(@"""BlockRound"""); break;
                    case "attestationPower":     columns.Add(@"""AttestationPower"""); break;
                    case "attestationCommittee": columns.Add(@"""AttestationCommittee"""); break;
                    case "rewardDelegated":      columns.Add(@"""RewardDelegated"""); break;
                    case "rewardStakedOwn":      columns.Add(@"""RewardStakedOwn"""); break;
                    case "rewardStakedEdge":     columns.Add(@"""RewardStakedEdge"""); break;
                    case "rewardStakedShared":   columns.Add(@"""RewardStakedShared"""); break;
                    case "bonusDelegated":       columns.Add(@"""BonusDelegated"""); break;
                    case "bonusStakedOwn":       columns.Add(@"""BonusStakedOwn"""); break;
                    case "bonusStakedEdge":      columns.Add(@"""BonusStakedEdge"""); break;
                    case "bonusStakedShared":    columns.Add(@"""BonusStakedShared"""); break;
                    case "bakerFees":            columns.Add(@"""BakerFees"""); break;
                    case "burnedFees":           columns.Add(@"""BurnedFees"""); columns.Add(@"""BurnedFees18"""); break;
                    case "proposer":             columns.Add(@"""ProposerId"""); break;
                    case "producer":             columns.Add(@"""ProducerId"""); break;
                    case "lBToggle":             columns.Add(@"""LBToggle"""); break;
                    case "lBToggleEma":          columns.Add(@"""LBToggleEma"""); break;
                    // XBlock
                    case "daFees":               columns.Add(@"""BakerFees18"""); break;
                    case "sequencerPool":        columns.Add(@"""ProposerId"""); break;
                    case "michelsonHash":        columns.Add(@"""MichelsonHash"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Blocks""")
            .Where(@"""Id""",            filter.Id)
            .Where(@"""ChainId""",       filter.Chain?.Id)
            .Where(@"""Level""",         filter.Level)
            .Where(@"""Timestamp""",     filter.Timestamp)
            .Where(@"""Hash""",          filter.Hash)
            .Where(@"""MichelsonHash""", filter.MichelsonHash)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(BlockFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.BlocksCount);

        ProcessFilters(filter);

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Blocks""")
            .Where(@"""Id""",            filter.Id)
            .Where(@"""ChainId""",       filter.Chain?.Id)
            .Where(@"""Level""",         filter.Level)
            .Where(@"""Timestamp""",     filter.Timestamp)
            .Where(@"""Hash""",          filter.Hash)
            .Where(@"""MichelsonHash""", filter.MichelsonHash)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    internal async Task<IEnumerable<(Data.Models.AllOperations Operations, Data.Models.AllBlockEvents Events)>> GetMasks(BlockFilter filter)
    {
        ProcessFilters(filter);

        var (query, parameters) = new SqlBuilder()
            .Select([@"""Operations""", @"""Events"""])
            .From(@"""Blocks""")
            .Where(@"""Id""",            filter.Id)
            .Where(@"""ChainId""",       filter.Chain?.Id)
            .Where(@"""Level""",         filter.Level)
            .Where(@"""Timestamp""",     filter.Timestamp)
            .Where(@"""Hash""",          filter.Hash)
            .Where(@"""MichelsonHash""", filter.MichelsonHash)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        var rows = await db.QueryAsync(query, parameters);

        return rows.Select(row => (
            (Data.Models.AllOperations)(long)row.Operations,
            (Data.Models.AllBlockEvents)(int)row.Events));
    }

    public async Task<IEnumerable<Block>> Get(BlockFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, Block>(row =>
        {
            if (row.Layer == (int)Data.Models.Layer.L1)
                return new L1Block
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Hash = row.Hash,
                    Timestamp = row.Timestamp,
                    Cycle = row.Cycle,
                    Protocol = _protocolCache.GetInfo((int)row.ProtocolId),
                    Software = _softwareCache.GetInfo((int?)row.SoftwareId),
                    PayloadRound = row.PayloadRound,
                    BlockRound = row.BlockRound,
                    AttestationPower = row.AttestationPower,
                    AttestationCommittee = row.AttestationCommittee,
                    RewardDelegated = row.RewardDelegated,
                    RewardStakedOwn = row.RewardStakedOwn,
                    RewardStakedEdge = row.RewardStakedEdge,
                    RewardStakedShared = row.RewardStakedShared,
                    BonusDelegated = row.BonusDelegated,
                    BonusStakedOwn = row.BonusStakedOwn,
                    BonusStakedEdge = row.BonusStakedEdge,
                    BonusStakedShared = row.BonusStakedShared,
                    BakerFees = row.BakerFees,
                    BurnedFees = row.BurnedFees,
                    Proposer = _addressCache.GetInfo((int?)row.ProposerId),
                    Producer = _addressCache.GetInfo((int?)row.ProducerId),
                    LBToggle = row.LBToggle,
                    LBToggleEma = row.LBToggleEma,
                };

            if (row.Layer == (int)Data.Models.Layer.TezosX)
                return new XBlock
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Hash = row.Hash,
                    Timestamp = row.Timestamp,
                    Protocol = _protocolCache.GetInfo((int)row.ProtocolId),
                    DaFees = (BigInteger)row.BakerFees18,
                    BurnedFees = (BigInteger)row.BurnedFees18,
                    SequencerPool = _addressCache.GetInfo((int?)row.ProposerId),
                    MichelsonHash = row.MichelsonHash,
                };

            throw new InvalidOperationException("Failed to read Block");
        });
    }

    public async Task<object?[][]> Get(BlockFilter filter, Pagination pagination, Selection selection)
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
                // Block
                case "id":
                    foreach (var row in rows) result[j++][i] = row.Id.ToString();
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
                case "level":
                    foreach (var row in rows) result[j++][i] = row.Level;
                    break;
                case "hash":
                    foreach (var row in rows) result[j++][i] = row.Hash;
                    break;
                case "timestamp":
                    foreach (var row in rows) result[j++][i] = row.Timestamp;
                    break;
                case "protocol":
                    foreach (var row in rows) result[j++][i] = _protocolCache.GetInfo((int)row.ProtocolId);
                    break;
                case "protocol.id":
                    foreach (var row in rows) result[j++][i] = row.ProtocolId;
                    break;
                case "protocol.hash":
                    foreach (var row in rows) result[j++][i] = _protocolCache.GetInfo((int)row.ProtocolId).Hash;
                    break;
                case "protocol.version":
                    foreach (var row in rows) result[j++][i] = _protocolCache.GetInfo((int)row.ProtocolId).Version;
                    break;
                // L1Block
                case "cycle":
                    foreach (var row in rows) result[j++][i] = row.Cycle;
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
                case "payloadRound":
                    foreach (var row in rows) result[j++][i] = row.PayloadRound;
                    break;
                case "blockRound":
                    foreach (var row in rows) result[j++][i] = row.BlockRound;
                    break;
                case "attestationPower":
                    foreach (var row in rows) result[j++][i] = row.AttestationPower;
                    break;
                case "attestationCommittee":
                    foreach (var row in rows) result[j++][i] = row.AttestationCommittee;
                    break;
                case "rewardDelegated":
                    foreach (var row in rows) result[j++][i] = row.RewardDelegated;
                    break;
                case "rewardStakedOwn":
                    foreach (var row in rows) result[j++][i] = row.RewardStakedOwn;
                    break;
                case "rewardStakedEdge":
                    foreach (var row in rows) result[j++][i] = row.RewardStakedEdge;
                    break;
                case "rewardStakedShared":
                    foreach (var row in rows) result[j++][i] = row.RewardStakedShared;
                    break;
                case "bonusDelegated":
                    foreach (var row in rows) result[j++][i] = row.BonusDelegated;
                    break;
                case "bonusStakedOwn":
                    foreach (var row in rows) result[j++][i] = row.BonusStakedOwn;
                    break;
                case "bonusStakedEdge":
                    foreach (var row in rows) result[j++][i] = row.BonusStakedEdge;
                    break;
                case "bonusStakedShared":
                    foreach (var row in rows) result[j++][i] = row.BonusStakedShared;
                    break;
                case "bakerFees":
                    foreach (var row in rows) result[j++][i] = row.BakerFees;
                    break;
                case "burnedFees":
                    foreach (var row in rows) result[j++][i] = row.BurnedFees ?? row.BurnedFees18;
                    break;
                case "proposer":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.ProposerId);
                    break;
                case "proposer.id":
                    foreach (var row in rows) result[j++][i] = row.ProposerId;
                    break;
                case "proposer.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProposerId))?.Hash;
                    break;
                case "proposer.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProposerId))?.Type;
                    break;
                case "proposer.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProposerId))?.Alias;
                    break;
                case "producer":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.ProducerId);
                    break;
                case "producer.id":
                    foreach (var row in rows) result[j++][i] = row.ProducerId;
                    break;
                case "producer.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProducerId))?.Hash;
                    break;
                case "producer.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProducerId))?.Type;
                    break;
                case "producer.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProducerId))?.Alias;
                    break;
                case "lBToggle":
                    foreach (var row in rows) result[j++][i] = row.LBToggle;
                    break;
                case "lBToggleEma":
                    foreach (var row in rows) result[j++][i] = row.LBToggleEma;
                    break;
                // XBlock
                case "daFees":
                    foreach (var row in rows) result[j++][i] = (object?)row.BakerFees18;
                    break;
                case "sequencerPool":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.ProposerId);
                    break;
                case "sequencerPool.id":
                    foreach (var row in rows) result[j++][i] = row.ProposerId;
                    break;
                case "sequencerPool.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProposerId))?.Hash;
                    break;
                case "sequencerPool.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProposerId))?.Type;
                    break;
                case "sequencerPool.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProposerId))?.Alias;
                    break;
                case "michelsonHash":
                    foreach (var row in rows) result[j++][i] = row.MichelsonHash;
                    break;
            }
        }

        return result;
    }
}