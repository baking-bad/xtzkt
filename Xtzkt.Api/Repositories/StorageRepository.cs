using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class StorageRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",    (@"s.""Id""",    "bigint") },
        { "level", (@"s.""Level""", "integer") },
    };

    async Task ProcessFilters(StorageFilter filter)
    {
        filter.Chain?.Id += filter.Chain.ChainId?.ToIdParameter(_chainCache);
        var chainId = filter.Chain?.Id?.Eq;

        if (filter.Contract?.Hash != null)
            filter.Contract.Id += await filter.Contract.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Contract?.Creator?.Hash != null)
            filter.Contract.Creator.Id += await filter.Contract.Creator.Hash.ToIdParameter(_addressCache, chainId);
    }

    async Task<IEnumerable<dynamic>> Query(StorageFilter filter, Pagination pagination, Selection? selection = null)
    {
        await ProcessFilters(filter);

        var columns = new HashSet<string>();
        if (selection != null)
        {
            var counter = 0;
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":            columns.Add(@"s.""Id"""); break;
                    case "chain":         columns.Add(@"s.""ChainId"""); break;
                    case "contract":      columns.Add(@"s.""ContractId"""); break;
                    case "level":         columns.Add(@"s.""Level"""); break;
                    case "current":       columns.Add(@"s.""Current"""); break;
                    case "rawValue":      columns.Add(@"s.""RawValue"""); break;
                    case "transactionId": columns.Add(@"s.""TransactionId"""); break;
                    case "originationId": columns.Add(@"s.""OriginationId"""); break;
                    case "migrationId":   columns.Add(@"s.""MigrationId"""); break;
                    case "value":
                        if (field.Path == null)
                        {
                            columns.Add(@"s.""JsonValue""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"s.""JsonValue"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }
        else
        {
            columns.Add(@"s.*");
        }

        var sql = new SqlBuilder()
            .Select(columns)
            .From(@"""Storages""", "s");

        if (filter.Contract?.TypeHash != null || filter.Contract?.CodeHash != null || filter.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"s.""ContractId""");

        var (query, parameters) = sql
            .Where(@"s.""Id""", filter.Id)
            .Where(@"s.""ChainId""", filter.Chain?.Id)
            .Where(@"s.""ContractId""", filter.Contract?.Id)
            .Where(@"c.""TypeHash""", filter.Contract?.TypeHash)
            .Where(@"c.""CodeHash""", filter.Contract?.CodeHash)
            .Where(@"c.""CreatorId""", filter.Contract?.Creator?.Id)
            .Where(@"s.""Level""", filter.Level)
            .Where(@"s.""Current""", filter.Current)
            .Where(@"s.""RawValue""", filter.RawValue)
            .Where(@"s.""JsonValue""", filter.Value)
            .Where(@"s.""TransactionId""", filter.TransactionId)
            .Where(@"s.""OriginationId""", filter.OriginationId)
            .Where(@"s.""MigrationId""", filter.MigrationId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(StorageFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.StorageCounter);

        await ProcessFilters(filter);

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Storages""", "s");

        if (filter.Contract?.TypeHash != null || filter.Contract?.CodeHash != null || filter.Contract?.Creator != null)
            sql.InnerJoin(@"""Addresses""", "c", @"""Id""", @"s.""ContractId""");

        var (query, parameters) = sql
            .Where(@"s.""Id""", filter.Id)
            .Where(@"s.""ChainId""", filter.Chain?.Id)
            .Where(@"s.""ContractId""", filter.Contract?.Id)
            .Where(@"c.""TypeHash""", filter.Contract?.TypeHash)
            .Where(@"c.""CodeHash""", filter.Contract?.CodeHash)
            .Where(@"c.""CreatorId""", filter.Contract?.Creator?.Id)
            .Where(@"s.""Level""", filter.Level)
            .Where(@"s.""Current""", filter.Current)
            .Where(@"s.""RawValue""", filter.RawValue)
            .Where(@"s.""JsonValue""", filter.Value)
            .Where(@"s.""TransactionId""", filter.TransactionId)
            .Where(@"s.""OriginationId""", filter.OriginationId)
            .Where(@"s.""MigrationId""", filter.MigrationId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Storage>> Get(StorageFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new Storage
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Contract = _addressCache.GetContractInfo((int)row.ContractId),
            Level = row.Level,
            Current = row.Current,
            RawValue = Micheline.FromBytes((byte[])row.RawValue),
            Value = row.JsonValue,
            TransactionId = row.TransactionId,
            OriginationId = row.OriginationId,
            MigrationId = row.MigrationId,
        });
    }

    public async Task<object?[][]> Get(StorageFilter filter, Pagination pagination, Selection selection)
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
                case "level":
                    foreach (var row in rows) result[j++][i] = row.Level;
                    break;
                case "current":
                    foreach (var row in rows) result[j++][i] = row.Current;
                    break;
                case "rawValue":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.RawValue);
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
                    if (fields[i].Field == "value")
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
