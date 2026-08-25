using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories.Operations;

public class MigrationRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"""Id""",        "bigint") },
        { "level",     (@"""Level""",     "integer") },
        { "timestamp", (@"""Timestamp""", "timestamptz") },
    };

    async Task<bool> ProcessFilters(MigrationOperationFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Account?.Hash != null)
            filter.Account.Id += await filter.Account.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(MigrationOperationFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        if (selection != null)
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "runtime":        columns.Add(@"""Runtime"""); break;
                    case "id":             columns.Add(@"""Id"""); break;
                    case "chain":          columns.Add(@"""ChainId"""); break;
                    case "level":          columns.Add(@"""Level"""); break;
                    case "timestamp":      columns.Add(@"""Timestamp"""); break;
                    case "kind":           columns.Add(@"""Kind"""); break;
                    case "account":        columns.Add(@"""AddressId"""); break;
                    case "balanceChange":  columns.Add(@"""BalanceChange"""); columns.Add(@"""BalanceChange18"""); columns.Add(@"""Runtime"""); break;
                    case "nonceChange":    columns.Add(@"""NonceChange"""); break;
                    case "tokenTransfers": columns.Add(@"""TokenTransfers"""); break;
                    case "bigMapUpdates":  columns.Add(@"""BigMapUpdates"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""MigrationOps""")
            .Where(@"""Id""",        filter.Id)
            .Where(@"""ChainId""",   filter.Chain?.Id)
            .Where(@"""Runtime""",   filter.Runtime)
            .Where(@"""Level""",     filter.Level)
            .Where(@"""Timestamp""", filter.Timestamp)
            .Where(@"""Kind""",      filter.Kind)
            .Where(@"""AddressId""", filter.Account?.Id)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(MigrationOperationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.MigrationOpsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""MigrationOps""")
            .Where(@"""Id""",        filter.Id)
            .Where(@"""ChainId""",   filter.Chain?.Id)
            .Where(@"""Runtime""",   filter.Runtime)
            .Where(@"""Level""",     filter.Level)
            .Where(@"""Timestamp""", filter.Timestamp)
            .Where(@"""Kind""",      filter.Kind)
            .Where(@"""AddressId""", filter.Account?.Id)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<MigrationOperation>> Get(MigrationOperationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, MigrationOperation>(row =>
        {
            return (Data.Models.Runtime)(int)row.Runtime switch
            {
                Data.Models.Runtime.Michelson => new MichelsonMigrationOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Kind = MigrationKinds.ToString((int)row.Kind),
                    Account = _addressCache.GetInfo((int)row.AddressId),
                    BalanceChange = row.BalanceChange,
                    TokenTransfers = row.TokenTransfers,
                    BigMapUpdates = row.BigMapUpdates,
                },
                Data.Models.Runtime.Evm => new EvmMigrationOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Kind = MigrationKinds.ToString((int)row.Kind),
                    Account = _addressCache.GetInfo((int)row.AddressId),
                    BalanceChange = row.BalanceChange18,
                    NonceChange = row.NonceChange,
                },
                _ => throw new InvalidOperationException("Failed to read MigrationOperation")
            };
        });
    }

    public async Task<object?[][]> Get(MigrationOperationFilter filter, Pagination pagination, Selection selection)
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
                case "runtime":
                    foreach (var row in rows) result[j++][i] = Runtimes.ToString((int)row.Runtime);
                    break;
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
                case "timestamp":
                    foreach (var row in rows) result[j++][i] = row.Timestamp;
                    break;
                case "kind":
                    foreach (var row in rows) result[j++][i] = MigrationKinds.ToString((int)row.Kind);
                    break;
                case "account":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.AddressId);
                    break;
                case "account.id":
                    foreach (var row in rows) result[j++][i] = row.AddressId;
                    break;
                case "account.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Hash;
                    break;
                case "account.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Type;
                    break;
                case "account.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Alias;
                    break;
                case "balanceChange":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Runtime)(int)row.Runtime switch
                    {
                        Data.Models.Runtime.Michelson => row.BalanceChange,
                        Data.Models.Runtime.Evm => row.BalanceChange18,
                        _ => throw new InvalidOperationException("Failed to read MigrationOperation")
                    };
                    break;
                case "nonceChange":
                    foreach (var row in rows) result[j++][i] = row.NonceChange;
                    break;
                case "tokenTransfers":
                    foreach (var row in rows) result[j++][i] = row.TokenTransfers;
                    break;
                case "bigMapUpdates":
                    foreach (var row in rows) result[j++][i] = row.BigMapUpdates;
                    break;
            }
        }

        return result;
    }

    public async Task<IEnumerable<IActivity>> Activity(
        List<Data.Models.Address> addresses,
        ActivityRole roles,
        ChainInfoParameter? chain,
        DateTimeParameter? timestamp,
        CursorPagination pagination)
    {
        if ((roles & ActivityRole.Target) == 0)
            return [];

        List<int>? accountIds = null;
        foreach (var address in addresses)
        {
            if (address.MigrationsCount == 0)
                continue;

            accountIds ??= new(addresses.Count);
            accountIds.Add(address.Id);
        }

        if (accountIds == null)
            return [];

        return await Get(
            new() { Account = new() { Id = new() { In = accountIds } }, Chain = chain, Timestamp = timestamp },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }

    public async Task<IEnumerable<IActivity>> Activity(Int32EqParameter level, ChainInfoParameter? chain, CursorPagination pagination)
    {
        return await Get(
            new() { Level = level.ToInt32Parameter(), Chain = chain },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }
}
