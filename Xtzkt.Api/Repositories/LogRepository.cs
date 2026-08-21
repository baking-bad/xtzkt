using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class LogRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"l.""Id""",        "bigint") },
        { "level",     (@"l.""Level""",     "integer") },
        { "timestamp", (@"l.""Timestamp""", "timestamptz") },
    };

    async Task<bool> ProcessFilters(LogFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Address?.Hash != null)
            filter.Address.Id += await filter.Address.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(LogFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        if (selection != null)
        {
            var counter = 0;
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "runtime":       columns.Add(@"l.""Runtime"""); break;
                    // Log
                    case "id":            columns.Add(@"l.""Id"""); break;
                    case "chain":         columns.Add(@"l.""ChainId"""); break;
                    case "level":         columns.Add(@"l.""Level"""); break;
                    case "timestamp":     columns.Add(@"l.""Timestamp"""); break;
                    case "address":          columns.Add(@"l.""AddressId"""); break;
                    case "contractTypeHash": columns.Add(@"l.""ContractTypeHash"""); break;
                    case "contractCodeHash": columns.Add(@"l.""ContractCodeHash"""); break;
                    case "name":          columns.Add(@"l.""Name"""); break;
                    case "payload":
                        if (field.Path == null)
                        {
                            columns.Add(@"l.""Payload""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"l.""Payload"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    case "guessed":       columns.Add(@"l.""Guessed"""); break;
                    // EvmLog
                    case "transactionId": columns.Add(@"l.""TransactionId"""); break;
                    case "originationId": columns.Add(@"l.""OriginationId"""); break;
                    case "depositId":   columns.Add(@"l.""DepositId"""); break;
                    case "topics":        columns.Add(@"l.""Topics"""); break;
                    case "data":          columns.Add(@"l.""Data"""); break;
                    // MichelsonLog
                    case "type":          columns.Add(@"l.""Type"""); break;
                    case "rawPayload":    columns.Add(@"l.""PayloadRaw"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }
        else
        {
            columns.Add(@"l.*");
        }

        var sql = new SqlBuilder()
            .Select(columns)
            .From(@"""Logs""", "l");

        var (query, parameters) = sql
            .Where(@"l.""Id""",               filter.Id)
            .Where(@"l.""ChainId""",          filter.Chain?.Id)
            .Where(@"l.""Runtime""",          filter.Runtime)
            .Where(@"l.""Level""",            filter.Level)
            .Where(@"l.""Timestamp""",        filter.Timestamp)
            .Where(@"l.""AddressId""",        filter.Address?.Id)
            .Where(@"l.""ContractTypeHash""", filter.ContractTypeHash)
            .Where(@"l.""ContractCodeHash""", filter.ContractCodeHash)
            .Where(@"l.""Name""",             filter.Name)
            .Where(@"l.""Payload""",          filter.Payload)
            .Where(@"l.""Guessed""",          filter.Guessed)
            .Where(@"l.""TransactionId""",    filter.TransactionId)
            .Where(@"l.""OriginationId""",    filter.OriginationId)
            .Where(@"l.""DepositId""",      filter.DepositId)
            .Where(@"l.""Topics""[1]",        filter.Topic0)
            .Where(@"l.""Topics""[2]",        filter.Topic1)
            .Where(@"l.""Topics""[3]",        filter.Topic2)
            .Where(@"l.""Topics""[4]",        filter.Topic3)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(LogFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.LogsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Logs""", "l");

        var (query, parameters) = sql
            .Where(@"l.""Id""",               filter.Id)
            .Where(@"l.""ChainId""",          filter.Chain?.Id)
            .Where(@"l.""Runtime""",          filter.Runtime)
            .Where(@"l.""Level""",            filter.Level)
            .Where(@"l.""Timestamp""",        filter.Timestamp)
            .Where(@"l.""AddressId""",        filter.Address?.Id)
            .Where(@"l.""ContractTypeHash""", filter.ContractTypeHash)
            .Where(@"l.""ContractCodeHash""", filter.ContractCodeHash)
            .Where(@"l.""Name""",             filter.Name)
            .Where(@"l.""Payload""",          filter.Payload)
            .Where(@"l.""Guessed""",          filter.Guessed)
            .Where(@"l.""TransactionId""",    filter.TransactionId)
            .Where(@"l.""OriginationId""",    filter.OriginationId)
            .Where(@"l.""DepositId""",      filter.DepositId)
            .Where(@"l.""Topics""[1]",        filter.Topic0)
            .Where(@"l.""Topics""[2]",        filter.Topic1)
            .Where(@"l.""Topics""[3]",        filter.Topic2)
            .Where(@"l.""Topics""[4]",        filter.Topic3)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Log>> Get(LogFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, Log>(row =>
        {
            if (row.Runtime == (int)Data.Models.Runtime.Evm)
                return new EvmLog
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Address = _addressCache.GetInfo((int)row.AddressId),
                    ContractTypeHash = row.ContractTypeHash,
                    ContractCodeHash = row.ContractCodeHash,
                    Name = row.Name,
                    Payload = row.Payload,
                    Guessed = row.Guessed,
                    TransactionId = row.TransactionId,
                    OriginationId = row.OriginationId,
                    DepositId = row.DepositId,
                    Topics = [.. (byte[][])row.Topics],
                    Data = row.Data,
                };

            if (row.Runtime == (int)Data.Models.Runtime.Michelson)
                return new MichelsonLog
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Address = _addressCache.GetInfo((int)row.AddressId),
                    ContractTypeHash = row.ContractTypeHash,
                    ContractCodeHash = row.ContractCodeHash,
                    Name = row.Name,
                    Payload = row.Payload,
                    Guessed = row.Guessed,
                    TransactionId = row.TransactionId,
                    Type = row.Type == null ? null : Micheline.FromBytes((byte[])row.Type),
                    RawPayload = row.PayloadRaw == null ? null : Micheline.FromBytes((byte[])row.PayloadRaw),
                };

            throw new InvalidOperationException("Failed to read Log");
        });
    }

    public async Task<object?[][]> Get(LogFilter filter, Pagination pagination, Selection selection)
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
                    foreach (var row in rows) result[j++][i] = Runtimes.ToString(row.Runtime);
                    break;
                // Log
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
                case "address":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.AddressId);
                    break;
                case "address.id":
                    foreach (var row in rows) result[j++][i] = row.AddressId;
                    break;
                case "address.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Hash;
                    break;
                case "address.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Type;
                    break;
                case "address.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Alias;
                    break;
                case "contractTypeHash":
                    foreach (var row in rows) result[j++][i] = row.ContractTypeHash;
                    break;
                case "contractCodeHash":
                    foreach (var row in rows) result[j++][i] = row.ContractCodeHash;
                    break;
                case "name":
                    foreach (var row in rows) result[j++][i] = row.Name;
                    break;
                case "payload":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Payload;
                    break;
                case "guessed":
                    foreach (var row in rows) result[j++][i] = row.Guessed;
                    break;
                // EvmLog
                case "transactionId":
                    foreach (var row in rows) result[j++][i] = row.TransactionId?.ToString();
                    break;
                case "originationId":
                    foreach (var row in rows) result[j++][i] = row.OriginationId?.ToString();
                    break;
                case "depositId":
                    foreach (var row in rows) result[j++][i] = row.DepositId?.ToString();
                    break;
                case "topics":
                    foreach (var row in rows) result[j++][i] = row.Topics == null ? null : ((byte[][])row.Topics).Select(Decode.ToHex).ToList();
                    break;
                case "data":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[]?)row.Data);
                    break;
                // MichelsonLog
                case "type":
                    foreach (var row in rows) result[j++][i] = row.Type == null ? null : Micheline.FromBytes((byte[])row.Type);
                    break;
                case "rawPayload":
                    foreach (var row in rows) result[j++][i] = row.PayloadRaw == null ? null : Micheline.FromBytes((byte[])row.PayloadRaw);
                    break;
                default:
                    if (fields[i].Field == "payload")
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
