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

public class DepositRepository(
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

    async Task<bool> ProcessFilters(DepositOperationFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Receiver?.Hash != null)
            filter.Receiver.Id += await filter.Receiver.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Proxy?.Hash != null)
            filter.Proxy.Id += await filter.Proxy.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(DepositOperationFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "hash":           columns.Add(@"""Hash"""); break;
                    case "status":         columns.Add(@"""Status"""); break;
                    case "inboxLevel":     columns.Add(@"""InboxLevel"""); break;
                    case "inboxMessageId": columns.Add(@"""InboxMessageId"""); break;
                    case "receiver":       columns.Add(@"""ReceiverId"""); break;
                    case "type":           columns.Add(@"""Type"""); break;
                    case "amount":         columns.Add(@"""Amount"""); columns.Add(@"""Amount18"""); columns.Add(@"""Runtime"""); break;
                    case "ticketHash":     columns.Add(@"""TicketHash"""); break;
                    case "proxy":          columns.Add(@"""ProxyId"""); break;
                    case "depositId":      columns.Add(@"""DepositId"""); break;
                    case "claimTransactionId": columns.Add(@"""ClaimTransactionId"""); break;
                    case "logsCount":      columns.Add(@"""LogsCount"""); break;
                    case "bridgeTicketTransfers": columns.Add(@"""BridgeTicketTransfers"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""DepositOps""")
            .Where(filter.Or)
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""Level""",          filter.Level)
            .Where(@"""Timestamp""",      filter.Timestamp)
            .Where(@"""Hash""",           filter.Hash)
            .Where(@"""Status""",         filter.Status)
            .Where(@"""InboxLevel""",     filter.InboxLevel)
            .Where(@"""InboxMessageId""", filter.InboxMessageId)
            .Where(@"""ReceiverId""",     filter.Receiver?.Id)
            .Where(@"""ProxyId""",        filter.Proxy?.Id)
            .Where(@"""Type""",           filter.Type)
            .Where(@"""TicketHash""",     filter.TicketHash)
            .Where(@"""DepositId""",      filter.DepositId)
            .Where(@"""ClaimTransactionId""", filter.ClaimTransactionId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(DepositOperationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().OfType<Data.Models.XChain>().Sum(x => x.DepositOpsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""DepositOps""")
            .Where(@"""Id""",         filter.Id)
            .Where(@"""ChainId""",    filter.Chain?.Id)
            .Where(@"""Level""",      filter.Level)
            .Where(@"""Timestamp""",  filter.Timestamp)
            .Where(@"""Hash""",       filter.Hash)
            .Where(@"""Status""",     filter.Status)
            .Where(@"""InboxLevel""", filter.InboxLevel)
            .Where(@"""InboxMessageId""", filter.InboxMessageId)
            .Where(@"""ReceiverId""", filter.Receiver?.Id)
            .Where(@"""ProxyId""",    filter.Proxy?.Id)
            .Where(@"""Type""",       filter.Type)
            .Where(@"""TicketHash""", filter.TicketHash)
            .Where(@"""DepositId""",  filter.DepositId)
            .Where(@"""ClaimTransactionId""", filter.ClaimTransactionId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<DepositOperation>> Get(DepositOperationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, DepositOperation>(row =>
        {
            return (Data.Models.Runtime)(int)row.Runtime switch
            {
                Data.Models.Runtime.Michelson => new XMichelsonDepositOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Status = OperationStatuses.ToString((int)row.Status),
                    InboxLevel = row.InboxLevel,
                    InboxMessageId = row.InboxMessageId,
                    Receiver = _addressCache.GetInfo((int)row.ReceiverId),
                    Type = DepositTypes.ToString((int)row.Type),
                    Amount = row.Amount,
                },
                Data.Models.Runtime.Evm => new XEvmDepositOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Status = OperationStatuses.ToString((int)row.Status),
                    InboxLevel = row.InboxLevel,
                    InboxMessageId = row.InboxMessageId,
                    Receiver = _addressCache.GetInfo((int)row.ReceiverId),
                    Type = DepositTypes.ToString((int)row.Type),
                    Amount = row.Amount18,
                    TicketHash = row.TicketHash,
                    Proxy = _addressCache.GetInfo((int?)row.ProxyId),
                    DepositId = row.DepositId,
                    ClaimTransactionId = row.ClaimTransactionId,
                    LogsCount = row.LogsCount,
                    BridgeTicketTransfers = row.BridgeTicketTransfers,
                },
                _ => throw new InvalidOperationException("Failed to read DepositOperation")
            };
        });
    }

    public async Task<object?[][]> Get(DepositOperationFilter filter, Pagination pagination, Selection selection)
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
                case "hash":
                    foreach (var row in rows) result[j++][i] = row.Hash;
                    break;
                case "status":
                    foreach (var row in rows) result[j++][i] = OperationStatuses.ToString((int)row.Status);
                    break;
                case "inboxLevel":
                    foreach (var row in rows) result[j++][i] = row.InboxLevel;
                    break;
                case "inboxMessageId":
                    foreach (var row in rows) result[j++][i] = row.InboxMessageId;
                    break;
                case "receiver":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.ReceiverId);
                    break;
                case "receiver.id":
                    foreach (var row in rows) result[j++][i] = row.ReceiverId;
                    break;
                case "receiver.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Hash;
                    break;
                case "receiver.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Type;
                    break;
                case "receiver.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Alias;
                    break;
                case "type":
                    foreach (var row in rows) result[j++][i] = DepositTypes.ToString((int)row.Type);
                    break;
                case "amount":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Runtime)(int)row.Runtime switch
                    {
                        Data.Models.Runtime.Michelson => row.Amount,
                        Data.Models.Runtime.Evm => row.Amount18,
                        _ => null
                    };
                    break;
                case "ticketHash":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[]?)row.TicketHash);
                    break;
                case "proxy":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.ProxyId);
                    break;
                case "proxy.id":
                    foreach (var row in rows) result[j++][i] = row.ProxyId;
                    break;
                case "proxy.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Hash;
                    break;
                case "proxy.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Type;
                    break;
                case "proxy.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Alias;
                    break;
                case "depositId":
                    foreach (var row in rows) result[j++][i] = row.DepositId;
                    break;
                case "claimTransactionId":
                    foreach (var row in rows) result[j++][i] = row.ClaimTransactionId?.ToString();
                    break;
                case "logsCount":
                    foreach (var row in rows) result[j++][i] = row.LogsCount;
                    break;
                case "bridgeTicketTransfers":
                    foreach (var row in rows) result[j++][i] = row.BridgeTicketTransfers;
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
        List<int>? receiverIds = null;
        List<int>? proxyIds = null;

        foreach (var address in addresses)
        {
            var count = address switch
            {
                Data.Models.XAddress a => a.DepositOpsCount,
                _ => 0,
            };
            if (count == 0)
                continue;

            if ((roles & ActivityRole.Target) != 0)
            {
                receiverIds ??= new(addresses.Count);
                receiverIds.Add(address.Id);
            }

            if ((roles & ActivityRole.Mention) != 0 && address is Data.Models.XEvmAddress)
            {
                proxyIds ??= new(addresses.Count);
                proxyIds.Add(address.Id);
            }
        }

        if (receiverIds == null && proxyIds == null)
            return [];

        var or = new OrParameter(
            (@"""ReceiverId""", receiverIds),
            (@"""ProxyId""", proxyIds));

        return await Get(
            new() { Or = or, Chain = chain, Timestamp = timestamp },
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
