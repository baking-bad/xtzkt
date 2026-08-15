using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class TicketTransferRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"tt.""Id""",        "bigint") },
        { "level",     (@"tt.""Level""",     "integer") },
        { "timestamp", (@"tt.""Timestamp""", "timestamptz") },
    };

    async Task ProcessFilters(TicketTransferFilter filter)
    {
        filter.Chain?.Id += filter.Chain.ChainId?.ToIdParameter(_chainCache);
        var chainId = filter.Chain?.Id?.Eq;

        if (filter.From?.Hash != null)
            filter.From.Id += await filter.From.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.To?.Hash != null)
            filter.To.Id += await filter.To.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Ticket?.Ticketer?.Hash != null)
            filter.Ticket.Ticketer.Id += await filter.Ticket.Ticketer.Hash.ToIdParameter(_addressCache, chainId);
    }

    async Task<IEnumerable<dynamic>> Query(TicketTransferFilter filter, Pagination pagination, Selection? selection = null)
    {
        await ProcessFilters(filter);

        var columns = new HashSet<string>();
        var counter = 0;
        if (selection == null)
        {
            columns.Add(@"tt.""Id""");
            columns.Add(@"tt.""ChainId""");
            columns.Add(@"tt.""Level""");
            columns.Add(@"tt.""Timestamp""");
            columns.Add(@"tt.""FromId""");
            columns.Add(@"tt.""ToId""");
            columns.Add(@"tt.""Amount""");
            columns.Add(@"tt.""TransactionId""");
            columns.Add(@"tt.""TransferTicketId""");
            columns.Add(@"tt.""SmartRollupExecuteId""");
            columns.Add(@"tt.""TicketId"" as ""Ticket_Id""");
            columns.Add(@"tt.""TicketerId"" as ""Ticket_TicketerId""");
            columns.Add(@"t.""RawType"" as ""Ticket_RawType""");
            columns.Add(@"t.""RawContent"" as ""Ticket_RawContent""");
            columns.Add(@"t.""JsonContent"" as ""Ticket_JsonContent""");
            columns.Add(@"t.""TypeHash"" as ""Ticket_TypeHash""");
            columns.Add(@"t.""ContentHash"" as ""Ticket_ContentHash""");
            columns.Add(@"t.""TotalSupply"" as ""Ticket_TotalSupply""");
        }
        else
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":                   columns.Add(@"tt.""Id"""); break;
                    case "chain":                columns.Add(@"tt.""ChainId"""); break;
                    case "level":                columns.Add(@"tt.""Level"""); break;
                    case "timestamp":            columns.Add(@"tt.""Timestamp"""); break;
                    case "from":                 columns.Add(@"tt.""FromId"""); break;
                    case "to":                   columns.Add(@"tt.""ToId"""); break;
                    case "amount":               columns.Add(@"tt.""Amount"""); break;
                    case "transactionId":        columns.Add(@"tt.""TransactionId"""); break;
                    case "transferTicketId":     columns.Add(@"tt.""TransferTicketId"""); break;
                    case "smartRollupExecuteId": columns.Add(@"tt.""SmartRollupExecuteId"""); break;
                    case "ticket":
                        if (field.Path == null)
                        {
                            columns.Add(@"tt.""TicketId"" as ""Ticket_Id""");
                            columns.Add(@"tt.""TicketerId"" as ""Ticket_TicketerId""");
                            columns.Add(@"t.""RawType"" as ""Ticket_RawType""");
                            columns.Add(@"t.""RawContent"" as ""Ticket_RawContent""");
                            columns.Add(@"t.""JsonContent"" as ""Ticket_JsonContent""");
                            columns.Add(@"t.""TypeHash"" as ""Ticket_TypeHash""");
                            columns.Add(@"t.""ContentHash"" as ""Ticket_ContentHash""");
                            columns.Add(@"t.""TotalSupply"" as ""Ticket_TotalSupply""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":          columns.Add(@"tt.""TicketId"" as ""Ticket_Id"""); break;
                                case "ticketer":    columns.Add(@"tt.""TicketerId"" as ""Ticket_TicketerId"""); break;
                                case "rawType":     columns.Add(@"t.""RawType"" as ""Ticket_RawType"""); break;
                                case "rawContent":  columns.Add(@"t.""RawContent"" as ""Ticket_RawContent"""); break;
                                case "typeHash":    columns.Add(@"t.""TypeHash"" as ""Ticket_TypeHash"""); break;
                                case "contentHash": columns.Add(@"t.""ContentHash"" as ""Ticket_ContentHash"""); break;
                                case "totalSupply": columns.Add(@"t.""TotalSupply"" as ""Ticket_TotalSupply"""); break;
                                case "content":
                                    if (subField.Path == null)
                                    {
                                        columns.Add(@"t.""JsonContent"" as ""Ticket_JsonContent""");
                                    }
                                    else
                                    {
                                        field.Column = $"c{counter++}";
                                        columns.Add($@"t.""JsonContent"" #> '{{{subField.PathString}}}' as {field.Column}");
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

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""TicketTransfers""", "tt")
            .InnerJoin(@"""Tickets""", "t", @"""Id""", @"tt.""TicketId""")
            .Where(filter.Or)
            .Where(filter.Anyof, x => x switch
            {
                "from" => @"tt.""FromId""",
                "to" => @"tt.""ToId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `from` and `to` fields only."),
            })
            .Where(@"tt.""Id""",                   filter.Id)
            .Where(@"tt.""ChainId""",              filter.Chain?.Id)
            .Where(@"tt.""Level""",                filter.Level)
            .Where(@"tt.""Timestamp""",            filter.Timestamp)
            .Where(@"tt.""TicketId""",             filter.Ticket?.Id)
            .Where(@"tt.""TicketerId""",           filter.Ticket?.Ticketer?.Id)
            .Where(@"t.""RawType""",               filter.Ticket?.RawType)
            .Where(@"t.""RawContent""",            filter.Ticket?.RawContent)
            .Where(@"t.""JsonContent""",           filter.Ticket?.Content)
            .Where(@"t.""TypeHash""",              filter.Ticket?.TypeHash)
            .Where(@"t.""ContentHash""",           filter.Ticket?.ContentHash)
            .Where(@"tt.""FromId""",               filter.From?.Id)
            .Where(@"tt.""ToId""",                 filter.To?.Id)
            .Where(@"tt.""Amount""",               filter.Amount)
            .Where(@"tt.""TransactionId""",        filter.TransactionId)
            .Where(@"tt.""TransferTicketId""",     filter.TransferTicketId)
            .Where(@"tt.""SmartRollupExecuteId""", filter.SmartRollupExecuteId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(TicketTransferFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TicketTransfersCount);

        await ProcessFilters(filter);

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""TicketTransfers""", "tt");

        if (filter.Ticket?.RawType != null || filter.Ticket?.RawContent != null || filter.Ticket?.Content != null ||
            filter.Ticket?.TypeHash != null || filter.Ticket?.ContentHash != null)
            sql.InnerJoin(@"""Tickets""", "t", @"""Id""", @"tt.""TicketId""");

        var (query, parameters) = sql
            .Where(filter.Anyof, x => x switch
            {
                "from" => @"tt.""FromId""",
                "to" => @"tt.""ToId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `from` and `to` fields only."),
            })
            .Where(@"tt.""Id""",                   filter.Id)
            .Where(@"tt.""ChainId""",              filter.Chain?.Id)
            .Where(@"tt.""Level""",                filter.Level)
            .Where(@"tt.""Timestamp""",            filter.Timestamp)
            .Where(@"tt.""TicketId""",             filter.Ticket?.Id)
            .Where(@"tt.""TicketerId""",           filter.Ticket?.Ticketer?.Id)
            .Where(@"t.""RawType""",               filter.Ticket?.RawType)
            .Where(@"t.""RawContent""",            filter.Ticket?.RawContent)
            .Where(@"t.""JsonContent""",           filter.Ticket?.Content)
            .Where(@"t.""TypeHash""",              filter.Ticket?.TypeHash)
            .Where(@"t.""ContentHash""",           filter.Ticket?.ContentHash)
            .Where(@"tt.""FromId""",               filter.From?.Id)
            .Where(@"tt.""ToId""",                 filter.To?.Id)
            .Where(@"tt.""Amount""",               filter.Amount)
            .Where(@"tt.""TransactionId""",        filter.TransactionId)
            .Where(@"tt.""TransferTicketId""",     filter.TransferTicketId)
            .Where(@"tt.""SmartRollupExecuteId""", filter.SmartRollupExecuteId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<TicketTransfer>> Get(TicketTransferFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new TicketTransfer
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = row.Level,
            Timestamp = row.Timestamp,
            Ticket = new TicketInfo
            {
                Id = row.Ticket_Id,
                Ticketer = _addressCache.GetInfo((int)row.Ticket_TicketerId),
                RawType = Micheline.FromBytes((byte[])row.Ticket_RawType),
                RawContent = Micheline.FromBytes((byte[])row.Ticket_RawContent),
                Content = row.Ticket_JsonContent,
                TypeHash = row.Ticket_TypeHash,
                ContentHash = row.Ticket_ContentHash,
                TotalSupply = row.Ticket_TotalSupply,
            },
            From = _addressCache.GetInfo((int?)row.FromId),
            To = _addressCache.GetInfo((int?)row.ToId),
            Amount = row.Amount,
            TransactionId = row.TransactionId,
            TransferTicketId = row.TransferTicketId,
            SmartRollupExecuteId = row.SmartRollupExecuteId,
        });
    }

    public async Task<object?[][]> Get(TicketTransferFilter filter, Pagination pagination, Selection selection)
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
                case "level":
                    foreach (var row in rows) result[j++][i] = row.Level;
                    break;
                case "timestamp":
                    foreach (var row in rows) result[j++][i] = row.Timestamp;
                    break;
                case "from":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.FromId);
                    break;
                case "from.id":
                    foreach (var row in rows) result[j++][i] = row.FromId;
                    break;
                case "from.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.FromId))?.Hash;
                    break;
                case "from.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.FromId))?.Type;
                    break;
                case "from.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.FromId))?.Alias;
                    break;
                case "to":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.ToId);
                    break;
                case "to.id":
                    foreach (var row in rows) result[j++][i] = row.ToId;
                    break;
                case "to.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ToId))?.Hash;
                    break;
                case "to.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ToId))?.Type;
                    break;
                case "to.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ToId))?.Alias;
                    break;
                case "amount":
                    foreach (var row in rows) result[j++][i] = row.Amount;
                    break;
                case "transactionId":
                    foreach (var row in rows) result[j++][i] = row.TransactionId?.ToString();
                    break;
                case "transferTicketId":
                    foreach (var row in rows) result[j++][i] = row.TransferTicketId?.ToString();
                    break;
                case "smartRollupExecuteId":
                    foreach (var row in rows) result[j++][i] = row.SmartRollupExecuteId?.ToString();
                    break;
                case "ticket":
                    foreach (var row in rows) result[j++][i] = new TicketInfo
                    {
                        Id = row.Ticket_Id,
                        Ticketer = _addressCache.GetInfo((int)row.Ticket_TicketerId),
                        RawType = Micheline.FromBytes((byte[])row.Ticket_RawType),
                        RawContent = Micheline.FromBytes((byte[])row.Ticket_RawContent),
                        Content = row.Ticket_JsonContent,
                        TypeHash = row.Ticket_TypeHash,
                        ContentHash = row.Ticket_ContentHash,
                        TotalSupply = row.Ticket_TotalSupply,
                    };
                    break;
                case "ticket.id":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Id?.ToString();
                    break;
                case "ticket.ticketer":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId);
                    break;
                case "ticket.ticketer.id":
                    foreach (var row in rows) result[j++][i] = row.Ticket_TicketerId;
                    break;
                case "ticket.ticketer.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Hash;
                    break;
                case "ticket.ticketer.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Type;
                    break;
                case "ticket.ticketer.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Alias;
                    break;
                case "ticket.rawType":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.Ticket_RawType);
                    break;
                case "ticket.rawContent":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.Ticket_RawContent);
                    break;
                case "ticket.content":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Ticket_JsonContent;
                    break;
                case "ticket.typeHash":
                    foreach (var row in rows) result[j++][i] = row.Ticket_TypeHash;
                    break;
                case "ticket.contentHash":
                    foreach (var row in rows) result[j++][i] = row.Ticket_ContentHash;
                    break;
                case "ticket.totalSupply":
                    foreach (var row in rows) result[j++][i] = row.Ticket_TotalSupply;
                    break;
                default:
                    if (fields[i].Full.StartsWith("ticket.content."))
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
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
        List<int>? fromIds = null;
        List<int>? toIds = null;

        foreach (var address in addresses)
        {
            if (address.TicketTransfersCount == 0)
                continue;

            if ((roles & ActivityRole.Sender) != 0)
            {
                fromIds ??= new(addresses.Count);
                fromIds.Add(address.Id);
            }

            if ((roles & ActivityRole.Target) != 0)
            {
                toIds ??= new(addresses.Count);
                toIds.Add(address.Id);
            }
        }

        if (fromIds == null && toIds == null)
            return [];

        var or = new OrParameter(
            (@"tt.""FromId""", fromIds),
            (@"tt.""ToId""", toIds));

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

    public async Task<IEnumerable<IOpgActivity>> Activity(List<long> transactionIds, List<long> transferTicketIds, CursorPagination pagination)
    {
        var tasks = new List<Task<IEnumerable<TicketTransfer>>>(2);

        if (transactionIds.Count != 0)
            tasks.Add(Get(
                new() { TransactionId = new() { In = transactionIds } },
                new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit }));

        if (transferTicketIds.Count != 0)
            tasks.Add(Get(
                new() { TransferTicketId = new() { In = transferTicketIds } },
                new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit }));

        await Task.WhenAll(tasks);

        return tasks.SelectMany(x => x.Result);
    }
}
