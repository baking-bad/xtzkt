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

public class RevealRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"""Id""",        "bigint") },
        { "level",     (@"""Level""",     "integer") },
        { "timestamp", (@"""Timestamp""", "timestamptz") },
    };

    async Task<bool> ProcessFilters(ManagerOperationFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Sender?.Hash != null)
            filter.Sender.Id += await filter.Sender.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(ManagerOperationFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "layer":         columns.Add(@"""Layer"""); break;
                    case "id":            columns.Add(@"""Id"""); break;
                    case "chain":         columns.Add(@"""ChainId"""); break;
                    case "level":         columns.Add(@"""Level"""); break;
                    case "timestamp":     columns.Add(@"""Timestamp"""); break;
                    case "hash":          columns.Add(@"""Hash"""); break;
                    case "sender":        columns.Add(@"""SenderId"""); break;
                    case "counter":       columns.Add(@"""Counter"""); break;
                    case "gasLimit":      columns.Add(@"""GasLimit"""); break;
                    case "gasUsed":       columns.Add(@"""GasUsed"""); break;
                    case "storageLimit":  columns.Add(@"""StorageLimit"""); break;
                    case "storageUsed":   columns.Add(@"""StorageUsed"""); break;
                    case "storageFee":    columns.Add(@"""StorageFee"""); break;
                    case "allocationFee": columns.Add(@"""AllocationFee"""); break;
                    case "status":        columns.Add(@"""Status"""); break;
                    case "errors":        columns.Add(@"""Errors"""); break;
                    case "bakerFee":      columns.Add(@"""BakerFee"""); break;
                    case "daFee":         columns.Add(@"""DaFee"""); break;
                    case "gasFee":        columns.Add(@"""GasFee"""); break;
                    case "gasRefund":     columns.Add(@"""GasRefund"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""RevealOps""")
            .Where(@"""Id""",        filter.Id)
            .Where(@"""ChainId""",   filter.Chain?.Id)
            .Where(@"""Level""",     filter.Level)
            .Where(@"""Timestamp""", filter.Timestamp)
            .Where(@"""Hash""",      filter.Hash)
            .Where(@"""SenderId""",  filter.Sender?.Id)
            .Where(@"""Counter""",   filter.Counter)
            .Where(@"""Status""",    filter.Status)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(ManagerOperationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.RevealOpsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""RevealOps""")
            .Where(@"""Id""",        filter.Id)
            .Where(@"""ChainId""",   filter.Chain?.Id)
            .Where(@"""Level""",     filter.Level)
            .Where(@"""Timestamp""", filter.Timestamp)
            .Where(@"""Hash""",      filter.Hash)
            .Where(@"""SenderId""",  filter.Sender?.Id)
            .Where(@"""Counter""",   filter.Counter)
            .Where(@"""Status""",    filter.Status)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<RevealOperation>> Get(ManagerOperationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, RevealOperation>(row =>
        {
            return (Data.Models.Layer)(int)row.Layer switch
            {
                Data.Models.Layer.L1 => new L1RevealOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    Counter = row.Counter,
                    StorageFee = row.StorageFee,
                    AllocationFee = row.AllocationFee,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    BakerFee = row.BakerFee,
                },
                Data.Models.Layer.TezosX => new XRevealOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    Counter = row.Counter,
                    StorageFee = row.StorageFee,
                    AllocationFee = row.AllocationFee,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    DaFee = row.DaFee,
                    GasFee = row.GasFee,
                    GasRefund = row.GasRefund,
                },
                _ => throw new InvalidOperationException("Failed to read RevealOperation")
            };
        });
    }

    public async Task<object?[][]> Get(ManagerOperationFilter filter, Pagination pagination, Selection selection)
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
                    foreach (var row in rows) result[j++][i] = Layers.ToString((int)row.Layer);
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
                case "hash":
                    foreach (var row in rows) result[j++][i] = row.Hash;
                    break;
                case "sender":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.SenderId);
                    break;
                case "sender.id":
                    foreach (var row in rows) result[j++][i] = row.SenderId;
                    break;
                case "sender.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.SenderId)).Hash;
                    break;
                case "sender.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.SenderId)).Type;
                    break;
                case "sender.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.SenderId)).Alias;
                    break;
                case "counter":
                    foreach (var row in rows) result[j++][i] = row.Counter;
                    break;
                case "gasLimit":
                    foreach (var row in rows) result[j++][i] = row.GasLimit;
                    break;
                case "gasUsed":
                    foreach (var row in rows) result[j++][i] = row.GasUsed;
                    break;
                case "storageLimit":
                    foreach (var row in rows) result[j++][i] = row.StorageLimit;
                    break;
                case "storageUsed":
                    foreach (var row in rows) result[j++][i] = row.StorageUsed;
                    break;
                case "storageFee":
                    foreach (var row in rows) result[j++][i] = row.StorageFee;
                    break;
                case "allocationFee":
                    foreach (var row in rows) result[j++][i] = row.AllocationFee;
                    break;
                case "status":
                    foreach (var row in rows) result[j++][i] = OperationStatuses.ToString((int)row.Status);
                    break;
                case "errors":
                    foreach (var row in rows) result[j++][i] = row.Errors;
                    break;
                case "bakerFee":
                    foreach (var row in rows) result[j++][i] = row.BakerFee;
                    break;
                case "daFee":
                    foreach (var row in rows) result[j++][i] = row.DaFee;
                    break;
                case "gasFee":
                    foreach (var row in rows) result[j++][i] = row.GasFee;
                    break;
                case "gasRefund":
                    foreach (var row in rows) result[j++][i] = row.GasRefund;
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
        if ((roles & ActivityRole.Sender) == 0)
            return [];

        List<int>? senderIds = null;
        foreach (var address in addresses)
        {
            var revealsCount = address switch
            {
                Data.Models.L1Address a => a.RevealsCount,
                Data.Models.XMichelsonUser a => a.RevealsCount,
                _ => 0,
            };
            if (revealsCount == 0)
                continue;

            senderIds ??= new(addresses.Count);
            senderIds.Add(address.Id);
        }

        if (senderIds == null)
            return [];

        return await Get(
            new() { Sender = new() { Id = new() { In = senderIds } }, Chain = chain, Timestamp = timestamp },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }

    public async Task<IEnumerable<IActivity>> Activity(Int32EqParameter level, ChainInfoParameter? chain, CursorPagination pagination)
    {
        return await Get(
            new() { Level = level.ToInt32Parameter(), Chain = chain },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }

    public async Task<IEnumerable<IOpgActivity>> Activity(OperationHashEqParameter hash, ChainInfoParameter? chain, CursorPagination pagination)
    {
        return await Get(
            new() { Hash = hash.ToOperationHashParameter(), Chain = chain },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }
}
