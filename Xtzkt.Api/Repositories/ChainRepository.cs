using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class ChainRepository(ChainCache _chainCache, NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id", (@"""Id""", "integer") },
    };

    async Task<IEnumerable<dynamic>> Query(ChainFilter filter, Pagination pagination, Selection? selection = null)
    {
        var columns = new HashSet<string>();
        if (selection != null)
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "layer": columns.Add(@"""Layer"""); break;
                    // Chain
                    case "id": columns.Add(@"""Id"""); break;
                    case "chainId": columns.Add(@"""ChainId"""); break;
                    case "network": columns.Add(@"""Network"""); break;
                    case "level": columns.Add(@"""Level"""); break;
                    case "timestamp": columns.Add(@"""Timestamp"""); break;
                    case "hash": columns.Add(@"""Hash"""); break;
                    case "knownLevel": columns.Add(@"""KnownLevel"""); break;
                    case "syncedAt": columns.Add(@"""SyncedAt"""); break;
                    // XChain
                    case "rollupAddress": columns.Add(@"""RollupAddress"""); break;
                    case "kernel": columns.Add(@"""Kernel"""); break;
                    case "kernelUpgrade": columns.Add(@"""KernelUpgrade"""); break;
                    case "kernelUpgradeTime": columns.Add(@"""KernelUpgradeTime"""); break;
                    case "michelsonActivationLevel": columns.Add(@"""MichelsonActivationLevel"""); break;
                    case "michelsonChainId": columns.Add(@"""MichelsonChainId"""); break;
                    case "michelsonProtocol": columns.Add(@"""MichelsonProtocol"""); break;
                    case "michelsonBlock": columns.Add(@"""MichelsonBlock"""); break;
                    // L1Chain
                    case "cycle": columns.Add(@"""Cycle"""); break;
                    case "protocol": columns.Add(@"""Protocol"""); break;
                    case "nextProtocol": columns.Add(@"""NextProtocol"""); break;
                    case "votingEpoch": columns.Add(@"""VotingEpoch"""); break;
                    case "votingPeriod": columns.Add(@"""VotingPeriod"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Chains""")
            .Where(@"""Id""", filter.Id)
            .Where(@"""ChainId""", filter.ChainId)
            .Where(@"""Layer""", filter.Layer)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(ChainFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Count;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Chains""")
            .Where(@"""Id""", filter.Id)
            .Where(@"""ChainId""", filter.ChainId)
            .Where(@"""Layer""", filter.Layer)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Chain>> Get(ChainFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, Chain>(row =>
        {
            if (row.Layer == (int)Data.Models.Layer.L1)
                return new L1Chain
                {
                    Id = row.Id,
                    ChainId = row.ChainId,
                    Network = row.Network,
                    Hash = row.Hash,
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    KnownLevel = row.KnownLevel,
                    SyncedAt = row.SyncedAt,
                    Cycle = row.Cycle,
                    NextProtocol = row.NextProtocol,
                    Protocol = row.Protocol,
                    VotingEpoch = row.VotingEpoch,
                    VotingPeriod = row.VotingPeriod,
                };

            if (row.Layer == (int)Data.Models.Layer.TezosX)
                return new XChain
                {
                    Id = row.Id,
                    ChainId = row.ChainId,
                    Network = row.Network,
                    Hash = row.Hash,
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    KnownLevel = row.KnownLevel,
                    SyncedAt = row.SyncedAt,
                    Kernel = row.Kernel,
                    KernelUpgrade = row.KernelUpgrade,
                    KernelUpgradeTime = row.KernelUpgradeTime,
                    MichelsonActivationLevel = row.MichelsonActivationLevel,
                    MichelsonBlock = row.MichelsonBlock,
                    MichelsonChainId = row.MichelsonChainId,
                    MichelsonProtocol = row.MichelsonProtocol,
                    RollupAddress = row.RollupAddress,
                };

            throw new InvalidOperationException("Failed to read Chain");
        });
    }

    public async Task<object?[][]> Get(ChainFilter filter, Pagination pagination, Selection selection)
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
                    foreach (var row in rows)
                        result[j++][i] = Layers.ToString(row.Layer);
                    break;
                // Chain
                case "id":
                    foreach (var row in rows) result[j++][i] = row.Id;
                    break;
                case "chainId":
                    foreach (var row in rows) result[j++][i] = row.ChainId;
                    break;
                case "network":
                    foreach (var row in rows) result[j++][i] = row.Network;
                    break;
                case "level":
                    foreach (var row in rows) result[j++][i] = row.Level;
                    break;
                case "timestamp":
                    foreach (var row in rows) result[j++][i] = row.Timestamp;
                    break;
                case "hash":
                    foreach (var row in rows) result[j++][i] = row.Hash;
                    break;
                case "knownLevel":
                    foreach (var row in rows) result[j++][i] = row.KnownLevel;
                    break;
                case "syncedAt":
                    foreach (var row in rows) result[j++][i] = row.SyncedAt;
                    break;
                // XChain
                case "rollupAddress":
                    foreach (var row in rows) result[j++][i] = row.RollupAddress;
                    break;
                case "kernel":
                    foreach (var row in rows) result[j++][i] = row.Kernel;
                    break;
                case "kernelUpgrade":
                    foreach (var row in rows) result[j++][i] = row.KernelUpgrade;
                    break;
                case "kernelUpgradeTime":
                    foreach (var row in rows) result[j++][i] = row.KernelUpgradeTime;
                    break;
                case "michelsonActivationLevel":
                    foreach (var row in rows) result[j++][i] = row.MichelsonActivationLevel;
                    break;
                case "michelsonChainId":
                    foreach (var row in rows) result[j++][i] = row.MichelsonChainId;
                    break;
                case "michelsonProtocol":
                    foreach (var row in rows) result[j++][i] = row.MichelsonProtocol;
                    break;
                case "michelsonBlock":
                    foreach (var row in rows) result[j++][i] = row.MichelsonBlock;
                    break;
                // L1Chain
                case "cycle":
                    foreach (var row in rows) result[j++][i] = row.Cycle;
                    break;
                case "protocol":
                    foreach (var row in rows) result[j++][i] = row.Protocol;
                    break;
                case "nextProtocol":
                    foreach (var row in rows) result[j++][i] = row.NextProtocol;
                    break;
                case "votingEpoch":
                    foreach (var row in rows) result[j++][i] = row.VotingEpoch;
                    break;
                case "votingPeriod":
                    foreach (var row in rows) result[j++][i] = row.VotingPeriod;
                    break;
            }
        }

        return result;
    }
}
