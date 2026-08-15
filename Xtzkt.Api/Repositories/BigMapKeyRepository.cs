using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class BigMapKeyRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"bk.""Id""",             "bigint") },
        { "firstLevel",     (@"bk.""FirstLevel""",     "integer") },
        { "firstTimestamp", (@"bk.""FirstTimestamp""", "timestamptz") },
        { "lastLevel",      (@"bk.""LastLevel""",      "integer") },
        { "lastTimestamp",  (@"bk.""LastTimestamp""",  "timestamptz") },
        { "updates",        (@"bk.""Updates""",        "integer") },
    };

    async Task<bool> ProcessFilters(BigMapKeyFilter filter)
    {
        filter.Chain?.Id += filter.Chain.ChainId?.ToIdParameter(_chainCache);
        var chainId = filter.Chain?.Id?.Eq;

        if (filter.BigMap?.Contract?.Hash != null)
            filter.BigMap.Contract.Id += await filter.BigMap.Contract.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.BigMap?.Contract?.Creator?.Hash != null)
            filter.BigMap.Contract.Creator.Id += await filter.BigMap.Contract.Creator.Hash.ToIdParameter(_addressCache, chainId);

        return await BigMapRepository.TryResolveIds(_dataSource, filter.Chain, filter.BigMap);
    }

    async Task<IEnumerable<dynamic>> Query(BigMapKeyFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        var counter = 0;
        if (selection == null)
        {
            columns.Add(@"bk.""Id""");
            columns.Add(@"bk.""ChainId""");
            columns.Add(@"bk.""Active""");
            columns.Add(@"bk.""KeyHash""");
            columns.Add(@"bk.""RawKey""");
            columns.Add(@"bk.""JsonKey""");
            columns.Add(@"bk.""RawValue""");
            columns.Add(@"bk.""JsonValue""");
            columns.Add(@"bk.""FirstLevel""");
            columns.Add(@"bk.""FirstTimestamp""");
            columns.Add(@"bk.""LastLevel""");
            columns.Add(@"bk.""LastTimestamp""");
            columns.Add(@"bk.""Updates""");
            columns.Add(@"bk.""BigMapId"" as ""BigMap_Id""");
            columns.Add(@"b.""Ptr"" as ""BigMap_Ptr""");
            columns.Add(@"b.""ContractId"" as ""BigMap_ContractId""");
            columns.Add(@"b.""StoragePath"" as ""BigMap_StoragePath""");
        }
        else
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":             columns.Add(@"bk.""Id"""); break;
                    case "chain":          columns.Add(@"bk.""ChainId"""); break;
                    case "active":         columns.Add(@"bk.""Active"""); break;
                    case "keyHash":        columns.Add(@"bk.""KeyHash"""); break;
                    case "rawKey":         columns.Add(@"bk.""RawKey"""); break;
                    case "rawValue":       columns.Add(@"bk.""RawValue"""); break;
                    case "firstLevel":     columns.Add(@"bk.""FirstLevel"""); break;
                    case "firstTimestamp": columns.Add(@"bk.""FirstTimestamp"""); break;
                    case "lastLevel":      columns.Add(@"bk.""LastLevel"""); break;
                    case "lastTimestamp":  columns.Add(@"bk.""LastTimestamp"""); break;
                    case "updates":        columns.Add(@"bk.""Updates"""); break;
                    case "key":
                        if (field.Path == null)
                        {
                            columns.Add(@"bk.""JsonKey""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"bk.""JsonKey"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    case "value":
                        if (field.Path == null)
                        {
                            columns.Add(@"bk.""JsonValue""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"bk.""JsonValue"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    case "bigMap":
                        if (field.Path == null)
                        {
                            columns.Add(@"bk.""BigMapId"" as ""BigMap_Id""");
                            columns.Add(@"b.""Ptr"" as ""BigMap_Ptr""");
                            columns.Add(@"b.""ContractId"" as ""BigMap_ContractId""");
                            columns.Add(@"b.""StoragePath"" as ""BigMap_StoragePath""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":          columns.Add(@"bk.""BigMapId"" as ""BigMap_Id"""); break;
                                case "ptr":         columns.Add(@"b.""Ptr"" as ""BigMap_Ptr"""); break;
                                case "contract":    columns.Add(@"b.""ContractId"" as ""BigMap_ContractId"""); break;
                                case "storagePath": columns.Add(@"b.""StoragePath"" as ""BigMap_StoragePath"""); break;
                                default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Full} doesn't exist");
                            }
                        }
                        break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var sql = new SqlBuilder()
            .Select(columns)
            .From(@"""BigMapKeys""", "bk")
            .InnerJoin(@"""BigMaps""", "b", @"""Id""", @"bk.""BigMapId""");

        if (filter.BigMap?.Contract?.TypeHash != null || filter.BigMap?.Contract?.CodeHash != null || filter.BigMap?.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"b.""ContractId""");

        var (query, parameters) = sql
            .Where(@"bk.""Id""",             filter.Id)
            .Where(@"bk.""ChainId""",        filter.Chain?.Id)
            .Where(@"bk.""BigMapId""",       filter.BigMap?.Id)
            .Where(@"b.""Ptr""",             filter.BigMap?.Ptr)
            .Where(@"b.""ContractId""",      filter.BigMap?.Contract?.Id)
            .Where(@"c.""TypeHash""",        filter.BigMap?.Contract?.TypeHash)
            .Where(@"c.""CodeHash""",        filter.BigMap?.Contract?.CodeHash)
            .Where(@"c.""CreatorId""",       filter.BigMap?.Contract?.Creator?.Id)
            .Where(@"b.""StoragePath""",     filter.BigMap?.StoragePath)
            .Where(@"bk.""Active""",         filter.Active)
            .Where(@"bk.""KeyHash""",        filter.KeyHash)
            .Where(@"bk.""RawKey""",         filter.RawKey)
            .Where(@"bk.""JsonKey""",        filter.Key)
            .Where(@"bk.""RawValue""",       filter.RawValue)
            .Where(@"bk.""JsonValue""",      filter.Value)
            .Where(@"bk.""FirstLevel""",     filter.FirstLevel)
            .Where(@"bk.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"bk.""LastLevel""",      filter.LastLevel)
            .Where(@"bk.""LastTimestamp""",  filter.LastTimestamp)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(BigMapKeyFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.BigMapKeyCounter);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""BigMapKeys""", "bk");

        if (filter.BigMap?.Ptr != null || filter.BigMap?.Contract != null || filter.BigMap?.StoragePath != null)
            sql.InnerJoin(@"""BigMaps""", "b", @"""Id""", @"bk.""BigMapId""");

        if (filter.BigMap?.Contract?.TypeHash != null || filter.BigMap?.Contract?.CodeHash != null || filter.BigMap?.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"b.""ContractId""");

        var (query, parameters) = sql
            .Where(@"bk.""Id""",             filter.Id)
            .Where(@"bk.""ChainId""",        filter.Chain?.Id)
            .Where(@"bk.""BigMapId""",       filter.BigMap?.Id)
            .Where(@"b.""Ptr""",             filter.BigMap?.Ptr)
            .Where(@"b.""ContractId""",      filter.BigMap?.Contract?.Id)
            .Where(@"c.""TypeHash""",        filter.BigMap?.Contract?.TypeHash)
            .Where(@"c.""CodeHash""",        filter.BigMap?.Contract?.CodeHash)
            .Where(@"c.""CreatorId""",       filter.BigMap?.Contract?.Creator?.Id)
            .Where(@"b.""StoragePath""",     filter.BigMap?.StoragePath)
            .Where(@"bk.""Active""",         filter.Active)
            .Where(@"bk.""KeyHash""",        filter.KeyHash)
            .Where(@"bk.""RawKey""",         filter.RawKey)
            .Where(@"bk.""JsonKey""",        filter.Key)
            .Where(@"bk.""RawValue""",       filter.RawValue)
            .Where(@"bk.""JsonValue""",      filter.Value)
            .Where(@"bk.""FirstLevel""",     filter.FirstLevel)
            .Where(@"bk.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"bk.""LastLevel""",      filter.LastLevel)
            .Where(@"bk.""LastTimestamp""",  filter.LastTimestamp)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<BigMapKey>> Get(BigMapKeyFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new BigMapKey
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            BigMap = new BigMapInfo
            {
                Id = row.BigMap_Id,
                Ptr = row.BigMap_Ptr,
                Contract = _addressCache.GetContractInfo((int)row.BigMap_ContractId),
                StoragePath = row.BigMap_StoragePath,
            },
            Active = row.Active,
            KeyHash = row.KeyHash,
            RawKey = Micheline.FromBytes((byte[])row.RawKey),
            Key = row.JsonKey,
            RawValue = Micheline.FromBytes((byte[])row.RawValue),
            Value = row.JsonValue,
            FirstLevel = row.FirstLevel,
            FirstTimestamp = row.FirstTimestamp,
            LastLevel = row.LastLevel,
            LastTimestamp = row.LastTimestamp,
            Updates = row.Updates,
        });
    }

    public async Task<object?[][]> Get(BigMapKeyFilter filter, Pagination pagination, Selection selection)
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
                case "bigMap":
                    foreach (var row in rows) result[j++][i] = new BigMapInfo
                    {
                        Id = row.BigMap_Id,
                        Ptr = row.BigMap_Ptr,
                        Contract = _addressCache.GetContractInfo((int)row.BigMap_ContractId),
                        StoragePath = row.BigMap_StoragePath,
                    };
                    break;
                case "bigMap.id":
                    foreach (var row in rows) result[j++][i] = row.BigMap_Id;
                    break;
                case "bigMap.ptr":
                    foreach (var row in rows) result[j++][i] = row.BigMap_Ptr;
                    break;
                case "bigMap.contract":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId);
                    break;
                case "bigMap.contract.id":
                    foreach (var row in rows) result[j++][i] = row.BigMap_ContractId;
                    break;
                case "bigMap.contract.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.BigMap_ContractId)).Hash;
                    break;
                case "bigMap.contract.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.BigMap_ContractId)).Type;
                    break;
                case "bigMap.contract.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.BigMap_ContractId)).Alias;
                    break;
                case "bigMap.contract.typeHash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId)).TypeHash;
                    break;
                case "bigMap.contract.codeHash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId)).CodeHash;
                    break;
                case "bigMap.contract.creator":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId)).Creator;
                    break;
                case "bigMap.contract.creator.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId)).Creator.Id;
                    break;
                case "bigMap.contract.creator.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId)).Creator.Hash;
                    break;
                case "bigMap.contract.creator.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId)).Creator.Type;
                    break;
                case "bigMap.contract.creator.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetContractInfoAsync((int)row.BigMap_ContractId)).Creator.Alias;
                    break;
                case "bigMap.storagePath":
                    foreach (var row in rows) result[j++][i] = row.BigMap_StoragePath;
                    break;
                case "active":
                    foreach (var row in rows) result[j++][i] = row.Active;
                    break;
                case "keyHash":
                    foreach (var row in rows) result[j++][i] = row.KeyHash;
                    break;
                case "rawKey":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.RawKey);
                    break;
                case "key":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.JsonKey;
                    break;
                case "rawValue":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.RawValue);
                    break;
                case "value":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.JsonValue;
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
                case "updates":
                    foreach (var row in rows) result[j++][i] = row.Updates;
                    break;
                default:
                    if (fields[i].Field == "key" || fields[i].Field == "value")
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
