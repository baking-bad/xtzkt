using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;
using Xtzkt.Data.Utils;

namespace Xtzkt.Api.Repositories;

public class BigMapUpdateRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"bu.""Id""",        "bigint") },
        { "level",     (@"bu.""Level""",     "integer") },
        { "timestamp", (@"bu.""Timestamp""", "timestamptz") },
    };

    async Task<bool> ProcessFilters(BigMapUpdateFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.BigMap?.Contract?.Hash != null)
            filter.BigMap.Contract.Id += await filter.BigMap.Contract.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.BigMap?.Contract?.Creator?.Hash != null)
            filter.BigMap.Contract.Creator.Id += await filter.BigMap.Contract.Creator.Hash.ToIdParameter(_addressCache, chainId);

        return await BigMapRepository.TryResolveIds(_dataSource, filter.Chain, filter.BigMap);
    }

    async Task<IEnumerable<dynamic>> Query(BigMapUpdateFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        var counter = 0;
        if (selection == null)
        {
            columns.Add(@"bu.""Id""");
            columns.Add(@"bu.""ChainId""");
            columns.Add(@"bu.""Level""");
            columns.Add(@"bu.""Timestamp""");
            columns.Add(@"bu.""Action""");
            columns.Add(@"bu.""RawValue""");
            columns.Add(@"bu.""JsonValue""");
            columns.Add(@"bu.""TransactionId""");
            columns.Add(@"bu.""OriginationId""");
            columns.Add(@"bu.""MigrationId""");
            columns.Add(@"bu.""BigMapId"" as ""BigMap_Id""");
            columns.Add(@"b.""Ptr"" as ""BigMap_Ptr""");
            columns.Add(@"b.""ContractId"" as ""BigMap_ContractId""");
            columns.Add(@"b.""StoragePath"" as ""BigMap_StoragePath""");
            columns.Add(@"bu.""BigMapKeyId"" as ""BigMapKey_Id""");
            columns.Add(@"bk.""KeyHash"" as ""BigMapKey_KeyHash""");
            columns.Add(@"bk.""RawKey"" as ""BigMapKey_RawKey""");
            columns.Add(@"bk.""JsonKey"" as ""BigMapKey_Key""");
        }
        else
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":            columns.Add(@"bu.""Id"""); break;
                    case "chain":         columns.Add(@"bu.""ChainId"""); break;
                    case "level":         columns.Add(@"bu.""Level"""); break;
                    case "timestamp":     columns.Add(@"bu.""Timestamp"""); break;
                    case "action":        columns.Add(@"bu.""Action"""); break;
                    case "rawValue":      columns.Add(@"bu.""RawValue"""); break;
                    case "transactionId": columns.Add(@"bu.""TransactionId"""); break;
                    case "originationId": columns.Add(@"bu.""OriginationId"""); break;
                    case "migrationId":   columns.Add(@"bu.""MigrationId"""); break;
                    case "value":
                        if (field.Path == null)
                        {
                            columns.Add(@"bu.""JsonValue""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"bu.""JsonValue"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    case "bigMap":
                        if (field.Path == null)
                        {
                            columns.Add(@"bu.""BigMapId"" as ""BigMap_Id""");
                            columns.Add(@"b.""Ptr"" as ""BigMap_Ptr""");
                            columns.Add(@"b.""ContractId"" as ""BigMap_ContractId""");
                            columns.Add(@"b.""StoragePath"" as ""BigMap_StoragePath""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":          columns.Add(@"bu.""BigMapId"" as ""BigMap_Id"""); break;
                                case "ptr":         columns.Add(@"b.""Ptr"" as ""BigMap_Ptr"""); break;
                                case "contract":    columns.Add(@"b.""ContractId"" as ""BigMap_ContractId"""); break;
                                case "storagePath": columns.Add(@"b.""StoragePath"" as ""BigMap_StoragePath"""); break;
                                default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Full} doesn't exist");
                            }
                        }
                        break;
                    case "bigMapKey":
                        if (field.Path == null)
                        {
                            columns.Add(@"bu.""BigMapKeyId"" as ""BigMapKey_Id""");
                            columns.Add(@"bk.""KeyHash"" as ""BigMapKey_KeyHash""");
                            columns.Add(@"bk.""RawKey"" as ""BigMapKey_RawKey""");
                            columns.Add(@"bk.""JsonKey"" as ""BigMapKey_Key""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":      columns.Add(@"bu.""BigMapKeyId"" as ""BigMapKey_Id"""); break;
                                case "keyHash": columns.Add(@"bk.""KeyHash"" as ""BigMapKey_KeyHash"""); break;
                                case "rawKey":  columns.Add(@"bk.""RawKey"" as ""BigMapKey_RawKey"""); break;
                                case "key":
                                    if (subField.Path == null)
                                    {
                                        columns.Add(@"bk.""JsonKey"" as ""BigMapKey_Key""");
                                    }
                                    else
                                    {
                                        field.Column = $"c{counter++}";
                                        columns.Add($@"bk.""JsonKey"" #> '{{{subField.PathString}}}' as {field.Column}");
                                    }
                                    break;
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
            .From(@"""BigMapUpdates""", "bu")
            .InnerJoin(@"""BigMaps""", "b", @"""Id""", @"bu.""BigMapId""")
            .LeftJoin(@"""BigMapKeys""", "bk", @"""Id""", @"bu.""BigMapKeyId""");

        if (filter.BigMap?.Contract?.TypeHash != null || filter.BigMap?.Contract?.CodeHash != null || filter.BigMap?.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"b.""ContractId""");

        var (query, parameters) = sql
            .Where(@"bu.""Id""",            filter.Id)
            .Where(@"bu.""ChainId""",       filter.Chain?.Id)
            .Where(@"bu.""BigMapId""",      filter.BigMap?.Id)
            .Where(@"b.""Ptr""",            filter.BigMap?.Ptr)
            .Where(@"b.""ContractId""",     filter.BigMap?.Contract?.Id)
            .Where(@"c.""TypeHash""",       filter.BigMap?.Contract?.TypeHash)
            .Where(@"c.""CodeHash""",       filter.BigMap?.Contract?.CodeHash)
            .Where(@"c.""CreatorId""",      filter.BigMap?.Contract?.Creator?.Id)
            .Where(@"b.""StoragePath""",    filter.BigMap?.StoragePath)
            .Where(@"bu.""BigMapKeyId""",   filter.BigMapKey?.Id)
            .Where(@"bk.""KeyHash""",       filter.BigMapKey?.KeyHash)
            .Where(@"bk.""RawKey""",        filter.BigMapKey?.RawKey)
            .Where(@"bk.""JsonKey""",       filter.BigMapKey?.Key)
            .Where(@"bu.""Action""",        filter.Action)
            .Where(@"bu.""RawValue""",      filter.RawValue)
            .Where(@"bu.""JsonValue""",     filter.Value)
            .Where(@"bu.""Level""",         filter.Level)
            .Where(@"bu.""Timestamp""",     filter.Timestamp)
            .Where(@"bu.""TransactionId""", filter.TransactionId)
            .Where(@"bu.""OriginationId""", filter.OriginationId)
            .Where(@"bu.""MigrationId""",   filter.MigrationId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(BigMapUpdateFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.BigMapUpdateCounter);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""BigMapUpdates""", "bu");

        if (filter.BigMap?.Ptr != null || filter.BigMap?.Contract != null || filter.BigMap?.StoragePath != null)
            sql.InnerJoin(@"""BigMaps""", "b", @"""Id""", @"bu.""BigMapId""");

        if (filter.BigMapKey?.KeyHash != null || filter.BigMapKey?.RawKey != null || filter.BigMapKey?.Key != null)
            sql.LeftJoin(@"""BigMapKeys""", "bk", @"""Id""", @"bu.""BigMapKeyId""");

        if (filter.BigMap?.Contract?.TypeHash != null || filter.BigMap?.Contract?.CodeHash != null || filter.BigMap?.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"b.""ContractId""");

        var (query, parameters) = sql
            .Where(@"bu.""Id""",            filter.Id)
            .Where(@"bu.""ChainId""",       filter.Chain?.Id)
            .Where(@"bu.""BigMapId""",      filter.BigMap?.Id)
            .Where(@"b.""Ptr""",            filter.BigMap?.Ptr)
            .Where(@"b.""ContractId""",     filter.BigMap?.Contract?.Id)
            .Where(@"c.""TypeHash""",       filter.BigMap?.Contract?.TypeHash)
            .Where(@"c.""CodeHash""",       filter.BigMap?.Contract?.CodeHash)
            .Where(@"c.""CreatorId""",      filter.BigMap?.Contract?.Creator?.Id)
            .Where(@"b.""StoragePath""",    filter.BigMap?.StoragePath)
            .Where(@"bu.""BigMapKeyId""",   filter.BigMapKey?.Id)
            .Where(@"bk.""KeyHash""",       filter.BigMapKey?.KeyHash)
            .Where(@"bk.""RawKey""",        filter.BigMapKey?.RawKey)
            .Where(@"bk.""JsonKey""",       filter.BigMapKey?.Key)
            .Where(@"bu.""Action""",        filter.Action)
            .Where(@"bu.""RawValue""",      filter.RawValue)
            .Where(@"bu.""JsonValue""",     filter.Value)
            .Where(@"bu.""Level""",         filter.Level)
            .Where(@"bu.""Timestamp""",     filter.Timestamp)
            .Where(@"bu.""TransactionId""", filter.TransactionId)
            .Where(@"bu.""OriginationId""", filter.OriginationId)
            .Where(@"bu.""MigrationId""",   filter.MigrationId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<BigMapUpdate>> Get(BigMapUpdateFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new BigMapUpdate
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
            Level = row.Level,
            Timestamp = row.Timestamp,
            Action = BigMapActions.ToString((int)row.Action),
            BigMapKey = row.BigMapKey_Id == null ? null : new BigMapKeyInfo
            {
                Id = row.BigMapKey_Id,
                KeyHash = Hashes.FormatExprHash(row.BigMapKey_KeyHash),
                RawKey = Micheline.FromBytes((byte[])row.BigMapKey_RawKey),
                Key = row.BigMapKey_Key,
            },
            RawValue = row.RawValue == null ? null : Micheline.FromBytes((byte[])row.RawValue),
            Value = row.JsonValue,
            TransactionId = row.TransactionId,
            OriginationId = row.OriginationId,
            MigrationId = row.MigrationId,
        });
    }

    public async Task<object?[][]> Get(BigMapUpdateFilter filter, Pagination pagination, Selection selection)
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
                case "bigMapKey":
                    foreach (var row in rows) result[j++][i] = row.BigMapKey_Id == null ? null : new BigMapKeyInfo
                    {
                        Id = row.BigMapKey_Id,
                        KeyHash = Hashes.FormatExprHash(row.BigMapKey_KeyHash),
                        RawKey = Micheline.FromBytes((byte[])row.BigMapKey_RawKey),
                        Key = row.BigMapKey_Key,
                    };
                    break;
                case "bigMapKey.id":
                    foreach (var row in rows) result[j++][i] = row.BigMapKey_Id?.ToString();
                    break;
                case "bigMapKey.keyHash":
                    foreach (var row in rows) result[j++][i] = row.BigMapKey_KeyHash == null ? null : Hashes.FormatExprHash(row.BigMapKey_KeyHash);
                    break;
                case "bigMapKey.rawKey":
                    foreach (var row in rows) result[j++][i] = row.BigMapKey_RawKey == null ? null : Micheline.FromBytes((byte[])row.BigMapKey_RawKey);
                    break;
                case "bigMapKey.key":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.BigMapKey_Key;
                    break;
                case "level":
                    foreach (var row in rows) result[j++][i] = row.Level;
                    break;
                case "timestamp":
                    foreach (var row in rows) result[j++][i] = row.Timestamp;
                    break;
                case "action":
                    foreach (var row in rows) result[j++][i] = BigMapActions.ToString((int)row.Action);
                    break;
                case "rawValue":
                    foreach (var row in rows) result[j++][i] = row.RawValue == null ? null : Micheline.FromBytes((byte[])row.RawValue);
                    break;
                case "value":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.JsonValue;
                    break;
                case "transactionId":
                    foreach (var row in rows) result[j++][i] = row.TransactionId?.ToString();
                    break;
                case "originationId":
                    foreach (var row in rows) result[j++][i] = row.OriginationId?.ToString();
                    break;
                case "migrationId":
                    foreach (var row in rows) result[j++][i] = row.MigrationId?.ToString();
                    break;
                default:
                    if (fields[i].Field == "value" || fields[i].Full.StartsWith("bigMapKey.key."))
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
