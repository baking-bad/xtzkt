using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class BigMapRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"b.""Id""",             "integer") },
        { "ptr",            (@"b.""Ptr""",            "integer") },
        { "firstLevel",     (@"b.""FirstLevel""",     "integer") },
        { "firstTimestamp", (@"b.""FirstTimestamp""", "timestamptz") },
        { "lastLevel",      (@"b.""LastLevel""",      "integer") },
        { "lastTimestamp",  (@"b.""LastTimestamp""",  "timestamptz") },
        { "totalKeys",      (@"b.""TotalKeys""",      "integer") },
        { "activeKeys",     (@"b.""ActiveKeys""",     "integer") },
        { "updates",        (@"b.""Updates""",        "integer") },
    };

    async Task<bool> ProcessFilters(BigMapFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Contract?.Hash != null)
            filter.Contract.Id += await filter.Contract.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Contract?.Creator?.Hash != null)
            filter.Contract.Creator.Id += await filter.Contract.Creator.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(BigMapFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "id":             columns.Add(@"b.""Id"""); break;
                    case "chain":          columns.Add(@"b.""ChainId"""); break;
                    case "ptr":            columns.Add(@"b.""Ptr"""); break;
                    case "contract":       columns.Add(@"b.""ContractId"""); break;
                    case "storagePath":    columns.Add(@"b.""StoragePath"""); break;
                    case "active":         columns.Add(@"b.""Active"""); break;
                    case "keyType":        columns.Add(@"b.""KeyType"""); break;
                    case "valueType":      columns.Add(@"b.""ValueType"""); break;
                    case "firstLevel":     columns.Add(@"b.""FirstLevel"""); break;
                    case "firstTimestamp": columns.Add(@"b.""FirstTimestamp"""); break;
                    case "lastLevel":      columns.Add(@"b.""LastLevel"""); break;
                    case "lastTimestamp":  columns.Add(@"b.""LastTimestamp"""); break;
                    case "totalKeys":      columns.Add(@"b.""TotalKeys"""); break;
                    case "activeKeys":     columns.Add(@"b.""ActiveKeys"""); break;
                    case "updates":        columns.Add(@"b.""Updates"""); break;
                    case "tags":           columns.Add(@"b.""Tags"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }
        else
        {
            columns.Add(@"b.*");
        }

        var sql = new SqlBuilder()
            .Select(columns)
            .From(@"""BigMaps""", "b");

        if (filter.Contract?.TypeHash != null || filter.Contract?.CodeHash != null || filter.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"b.""ContractId""");

        var (query, parameters) = sql
            .Where(@"b.""Id""",             filter.Id)
            .Where(@"b.""ChainId""",        filter.Chain?.Id)
            .Where(@"b.""Ptr""",            filter.Ptr)
            .Where(@"b.""ContractId""",     filter.Contract?.Id)
            .Where(@"c.""TypeHash""",       filter.Contract?.TypeHash)
            .Where(@"c.""CodeHash""",       filter.Contract?.CodeHash)
            .Where(@"c.""CreatorId""",      filter.Contract?.Creator?.Id)
            .Where(@"b.""StoragePath""",    filter.StoragePath)
            .Where(@"b.""Active""",         filter.Active)
            .Where(@"b.""FirstLevel""",     filter.FirstLevel)
            .Where(@"b.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"b.""LastLevel""",      filter.LastLevel)
            .Where(@"b.""LastTimestamp""",  filter.LastTimestamp)
            .Where(@"b.""Tags""",           filter.Tags)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(BigMapFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.BigMapCounter);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""BigMaps""", "b");

        if (filter.Contract?.TypeHash != null || filter.Contract?.CodeHash != null || filter.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"b.""ContractId""");

        var (query, parameters) = sql
            .Where(@"b.""Id""",             filter.Id)
            .Where(@"b.""ChainId""",        filter.Chain?.Id)
            .Where(@"b.""Ptr""",            filter.Ptr)
            .Where(@"b.""ContractId""",     filter.Contract?.Id)
            .Where(@"c.""TypeHash""",       filter.Contract?.TypeHash)
            .Where(@"c.""CodeHash""",       filter.Contract?.CodeHash)
            .Where(@"c.""CreatorId""",      filter.Contract?.Creator?.Id)
            .Where(@"b.""StoragePath""",    filter.StoragePath)
            .Where(@"b.""Active""",         filter.Active)
            .Where(@"b.""FirstLevel""",     filter.FirstLevel)
            .Where(@"b.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"b.""LastLevel""",      filter.LastLevel)
            .Where(@"b.""LastTimestamp""",  filter.LastTimestamp)
            .Where(@"b.""Tags""",           filter.Tags)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<BigMap>> Get(BigMapFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new BigMap
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Ptr = row.Ptr,
            Contract = _addressCache.GetContractInfo((int)row.ContractId),
            StoragePath = row.StoragePath,
            Active = row.Active,
            KeyType = Micheline.FromBytes((byte[])row.KeyType),
            ValueType = Micheline.FromBytes((byte[])row.ValueType),
            FirstLevel = row.FirstLevel,
            FirstTimestamp = row.FirstTimestamp,
            LastLevel = row.LastLevel,
            LastTimestamp = row.LastTimestamp,
            TotalKeys = row.TotalKeys,
            ActiveKeys = row.ActiveKeys,
            Updates = row.Updates,
            Tags = BigMapTags.ToList((int)row.Tags),
        });
    }

    public async Task<object?[][]> Get(BigMapFilter filter, Pagination pagination, Selection selection)
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
                case "ptr":
                    foreach (var row in rows) result[j++][i] = row.Ptr;
                    break;
                case "contract":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetContractInfoAsync((int)row.ContractId);
                    break;
                case "contract.id":
                    foreach (var row in rows) result[j++][i] = row.ContractId;
                    break;
                case "contract.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ContractId)).Hash;
                    break;
                case "contract.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ContractId)).Type;
                    break;
                case "contract.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ContractId)).Alias;
                    break;
                case "contract.typeHash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.ContractId)).TypeHash;
                    break;
                case "contract.codeHash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.ContractId)).CodeHash;
                    break;
                case "contract.creator":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.ContractId)).Creator;
                    break;
                case "contract.creator.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.ContractId)).Creator.Id;
                    break;
                case "contract.creator.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.ContractId)).Creator.Hash;
                    break;
                case "contract.creator.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.ContractId)).Creator.Type;
                    break;
                case "contract.creator.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.ContractId)).Creator.Alias;
                    break;
                case "storagePath":
                    foreach (var row in rows) result[j++][i] = row.StoragePath;
                    break;
                case "active":
                    foreach (var row in rows) result[j++][i] = row.Active;
                    break;
                case "keyType":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.KeyType);
                    break;
                case "valueType":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.ValueType);
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
                case "totalKeys":
                    foreach (var row in rows) result[j++][i] = row.TotalKeys;
                    break;
                case "activeKeys":
                    foreach (var row in rows) result[j++][i] = row.ActiveKeys;
                    break;
                case "updates":
                    foreach (var row in rows) result[j++][i] = row.Updates;
                    break;
                case "tags":
                    foreach (var row in rows) result[j++][i] = BigMapTags.ToList((int)row.Tags);
                    break;
            }
        }

        return result;
    }

    #region helpers
    const int MaxResolvedIds = 257;

    /// <summary>
    /// Replaces bigmap property filters with the ids of the matching bigmaps.
    /// Returns `false` if there are no such bigmaps at all, so the caller can return an empty result.
    /// </summary>
    public static async Task<bool> TryResolveIds(NpgsqlDataSource dataSource, ChainInfoParameter? chain, BigMapInfoParameter? bigMap)
    {
        if (bigMap == null || bigMap.Ptr == null && bigMap.Contract == null && bigMap.StoragePath == null)
            return true;

        var sql = new SqlBuilder()
            .Select(@"b.""Id""")
            .From(@"""BigMaps""", "b");

        if (bigMap.Contract?.TypeHash != null || bigMap.Contract?.CodeHash != null || bigMap.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"b.""ContractId""");

        var (query, parameters) = sql
            .Where(@"b.""Id""", bigMap.Id)
            .Where(@"b.""ChainId""", chain?.Id)
            .Where(@"b.""Ptr""", bigMap.Ptr)
            .Where(@"b.""ContractId""", bigMap.Contract?.Id)
            .Where(@"c.""TypeHash""", bigMap.Contract?.TypeHash)
            .Where(@"c.""CodeHash""", bigMap.Contract?.CodeHash)
            .Where(@"c.""CreatorId""", bigMap.Contract?.Creator?.Id)
            .Where(@"b.""StoragePath""", bigMap.StoragePath)
            .Limit(MaxResolvedIds + 1)
            .Build();

        await using var db = await dataSource.OpenConnectionAsync();
        var ids = (await db.QueryAsync<int>(query, parameters)).ToList();

        if (ids.Count == 0)
            return false;

        // on a large id array `= ANY` stops being cheaper than the joins
        if (ids.Count <= MaxResolvedIds)
        {
            bigMap.Id = new Int32Parameter { In = ids };
            bigMap.Ptr = null;
            bigMap.Contract = null;
            bigMap.StoragePath = null;
        }

        return true;
    }
    #endregion
}
