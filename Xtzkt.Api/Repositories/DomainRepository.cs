using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class DomainRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",            (@"""Id""",            "bigint") },
        { "lastTimestamp", (@"""LastTimestamp""", "timestamptz") },
        { "expiration",    (@"""Expiration""",    "timestamptz") },
    };

    async Task<bool> ProcessFilters(DomainFilter filter, Pagination? pagination = null)
    {
        #region replace chain filter
        var chains = _chainCache.Resolve(filter.Chain);
        if (chains.Count == 0)
            return false;

        if (chains.Count != _chainCache.Count())
        {
            var idRange = _chainCache.GetId64Range(chains);
            if (!Int64Parameter.TryMerge(filter.Id, idRange, out var id))
                return false;
            filter.Id = id;

            if (filter.FirstTimestamp != null)
            {
                var timestampRange = _chainCache.GetTimestampRange(chains);
                if (!DateTimeParameter.TryMerge(filter.FirstTimestamp, timestampRange, out var firstTimestamp))
                    return false;
                filter.FirstTimestamp = firstTimestamp;
            }

            if (filter.LastTimestamp != null || pagination?.SortingBy("lastTimestamp") == true)
            {
                var timestampRange = _chainCache.GetTimestampRange(chains);
                if (!DateTimeParameter.TryMerge(filter.LastTimestamp, timestampRange, out var lastTimestamp))
                    return false;
                filter.LastTimestamp = lastTimestamp;
            }
        }
        #endregion

        return await _addressCache.Resolve(filter.Registry, chains);
    }

    async Task<IEnumerable<dynamic>> Query(DomainFilter filter, Pagination pagination, Selection? selection = null)
    {
        pagination.Reduce(SortSpec);

        if (!await ProcessFilters(filter, pagination))
            return [];

        var columns = new HashSet<string>();
        if (selection != null)
        {
            var counter = 0;
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":             columns.Add(@"""Id"""); break;
                    case "chain":          columns.Add(@"""ChainId"""); break;
                    case "registry":       columns.Add(@"""RegistryId"""); break;
                    case "level":          columns.Add(@"""Level"""); break;
                    case "name":           columns.Add(@"""Name"""); break;
                    case "owner":          columns.Add(@"""Owner"""); break;
                    case "address":        columns.Add(@"""Address"""); break;
                    case "reverse":        columns.Add(@"""Reverse"""); break;
                    case "expiration":     columns.Add(@"""Expiration"""); break;
                    case "firstLevel":     columns.Add(@"""FirstLevel"""); break;
                    case "firstTimestamp": columns.Add(@"""FirstTimestamp"""); break;
                    case "lastLevel":      columns.Add(@"""LastLevel"""); break;
                    case "lastTimestamp":  columns.Add(@"""LastTimestamp"""); break;
                    case "data":
                        if (field.Path == null)
                        {
                            columns.Add(@"""Data""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"""Data"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Domains""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""RegistryId""",     filter.Registry?.Id)
            .Where(@"""Level""",          filter.Level)
            .Where(@"""Name""",           filter.Name)
            .Where(@"""Owner""",          filter.Owner)
            .Where(@"""Address""",        filter.Address)
            .Where(@"""Reverse""",        filter.Reverse)
            .Where(@"""Expiration""",     filter.Expiration)
            .Where(@"""Data""",           filter.Data)
            .Where(@"""FirstLevel""",     filter.FirstLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastLevel""",      filter.LastLevel)
            .Where(@"""LastTimestamp""",  filter.LastTimestamp)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(DomainFilter filter)
    {
        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Domains""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""RegistryId""",     filter.Registry?.Id)
            .Where(@"""Level""",          filter.Level)
            .Where(@"""Name""",           filter.Name)
            .Where(@"""Owner""",          filter.Owner)
            .Where(@"""Address""",        filter.Address)
            .Where(@"""Reverse""",        filter.Reverse)
            .Where(@"""Expiration""",     filter.Expiration)
            .Where(@"""Data""",           filter.Data)
            .Where(@"""FirstLevel""",     filter.FirstLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastLevel""",      filter.LastLevel)
            .Where(@"""LastTimestamp""",  filter.LastTimestamp)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Domain>> Get(DomainFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new Domain
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Registry = _addressCache.GetInfo((int)row.RegistryId),
            Level = row.Level,
            Name = row.Name,
            Owner = row.Owner,
            Address = row.Address,
            Reverse = row.Reverse,
            Expiration = row.Expiration,
            Data = row.Data,
            FirstLevel = row.FirstLevel,
            FirstTimestamp = row.FirstTimestamp,
            LastLevel = row.LastLevel,
            LastTimestamp = row.LastTimestamp,
        });
    }

    public async Task<object?[][]> Get(DomainFilter filter, Pagination pagination, Selection selection)
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
                case "registry":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.RegistryId);
                    break;
                case "registry.id":
                    foreach (var row in rows) result[j++][i] = row.RegistryId;
                    break;
                case "registry.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.RegistryId)).Hash;
                    break;
                case "registry.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.RegistryId)).Type;
                    break;
                case "registry.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.RegistryId)).Alias;
                    break;
                case "registry.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.RegistryId)).Domain;
                    break;
                case "level":
                    foreach (var row in rows) result[j++][i] = row.Level;
                    break;
                case "name":
                    foreach (var row in rows) result[j++][i] = row.Name;
                    break;
                case "owner":
                    foreach (var row in rows) result[j++][i] = row.Owner;
                    break;
                case "address":
                    foreach (var row in rows) result[j++][i] = row.Address;
                    break;
                case "reverse":
                    foreach (var row in rows) result[j++][i] = row.Reverse;
                    break;
                case "expiration":
                    foreach (var row in rows) result[j++][i] = row.Expiration;
                    break;
                case "data":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Data;
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
                default:
                    if (fields[i].Field == "data")
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
