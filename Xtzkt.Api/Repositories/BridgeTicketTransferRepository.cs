using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class BridgeTicketTransferRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"tt.""Id""",        "bigint") },
        { "level",     (@"tt.""Level""",     "integer") },
        { "timestamp", (@"tt.""Timestamp""", "timestamptz") },
    };

    async Task<bool> ProcessFilters(BridgeTicketTransferFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.From?.Hash != null)
            filter.From.Id += await filter.From.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.To?.Hash != null)
            filter.To.Id += await filter.To.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(BridgeTicketTransferFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
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
            columns.Add(@"tt.""DepositId""");
            columns.Add(@"tt.""TicketId"" as ""Ticket_Id""");
            columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash""");
        }
        else
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":             columns.Add(@"tt.""Id"""); break;
                    case "chain":          columns.Add(@"tt.""ChainId"""); break;
                    case "level":          columns.Add(@"tt.""Level"""); break;
                    case "timestamp":      columns.Add(@"tt.""Timestamp"""); break;
                    case "from":           columns.Add(@"tt.""FromId"""); break;
                    case "to":             columns.Add(@"tt.""ToId"""); break;
                    case "amount":         columns.Add(@"tt.""Amount"""); break;
                    case "transactionId":  columns.Add(@"tt.""TransactionId"""); break;
                    case "depositId":    columns.Add(@"tt.""DepositId"""); break;
                    case "ticket":
                        if (field.Path == null)
                        {
                            columns.Add(@"tt.""TicketId"" as ""Ticket_Id""");
                            columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":          columns.Add(@"tt.""TicketId"" as ""Ticket_Id"""); break;
                                case "weakHash":    columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash"""); break;
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
            .From(@"""BridgeTicketTransfers""", "tt")
            .InnerJoin(@"""BridgeTickets""", "t", @"""Id""", @"tt.""TicketId""")
            .Where(filter.Or)
            .Where(filter.Anyof, x => x switch
            {
                "from" => @"tt.""FromId""",
                "to" => @"tt.""ToId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `from` and `to` fields only."),
            })
            .Where(@"tt.""Id""",             filter.Id)
            .Where(@"tt.""ChainId""",        filter.Chain?.Id)
            .Where(@"tt.""Level""",          filter.Level)
            .Where(@"tt.""Timestamp""",      filter.Timestamp)
            .Where(@"tt.""TicketId""",       filter.Ticket?.Id)
            .Where(@"t.""WeakHash""",        filter.Ticket?.WeakHash)
            .Where(@"tt.""FromId""",         filter.From?.Id)
            .Where(@"tt.""ToId""",           filter.To?.Id)
            .Where(@"tt.""Amount""",         filter.Amount)
            .Where(@"tt.""TransactionId""",  filter.TransactionId)
            .Where(@"tt.""DepositId""",    filter.DepositId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(BridgeTicketTransferFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().OfType<Data.Models.XChain>().Sum(x => x.BridgeTicketTransfersCount);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""BridgeTicketTransfers""", "tt");

        if (filter.Ticket?.WeakHash != null)
            sql.InnerJoin(@"""BridgeTickets""", "t", @"""Id""", @"tt.""TicketId""");

        var (query, parameters) = sql
            .Where(filter.Anyof, x => x switch
            {
                "from" => @"tt.""FromId""",
                "to" => @"tt.""ToId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `from` and `to` fields only."),
            })
            .Where(@"tt.""Id""",             filter.Id)
            .Where(@"tt.""ChainId""",        filter.Chain?.Id)
            .Where(@"tt.""Level""",          filter.Level)
            .Where(@"tt.""Timestamp""",      filter.Timestamp)
            .Where(@"tt.""TicketId""",       filter.Ticket?.Id)
            .Where(@"t.""WeakHash""",        filter.Ticket?.WeakHash)
            .Where(@"tt.""FromId""",         filter.From?.Id)
            .Where(@"tt.""ToId""",           filter.To?.Id)
            .Where(@"tt.""Amount""",         filter.Amount)
            .Where(@"tt.""TransactionId""",  filter.TransactionId)
            .Where(@"tt.""DepositId""",    filter.DepositId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<BridgeTicketTransfer>> Get(BridgeTicketTransferFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new BridgeTicketTransfer
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = row.Level,
            Timestamp = row.Timestamp,
            Ticket = new BridgeTicketInfo
            {
                Id = row.Ticket_Id,
                WeakHash = row.Ticket_WeakHash,
            },
            From = _addressCache.GetInfo((int?)row.FromId),
            To = _addressCache.GetInfo((int?)row.ToId),
            Amount = row.Amount,
            TransactionId = row.TransactionId,
            DepositId = row.DepositId,
        });
    }

    public async Task<object?[][]> Get(BridgeTicketTransferFilter filter, Pagination pagination, Selection selection)
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
                case "depositId":
                    foreach (var row in rows) result[j++][i] = row.DepositId?.ToString();
                    break;
                case "ticket":
                    foreach (var row in rows) result[j++][i] = new BridgeTicketInfo
                    {
                        Id = row.Ticket_Id,
                        WeakHash = row.Ticket_WeakHash,
                    };
                    break;
                case "ticket.id":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Id?.ToString();
                    break;
                case "ticket.weakHash":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[])row.Ticket_WeakHash);
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
            var count = address switch
            {
                Data.Models.XEvmAddress a => a.BridgeTicketTransfersCount,
                _ => 0,
            };
            if (count == 0)
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

    public async Task<IEnumerable<IOpgActivity>> Activity(List<long> transactionIds, List<long> depositIds, CursorPagination pagination)
    {
        var tasks = new List<Task<IEnumerable<BridgeTicketTransfer>>>(2);

        if (transactionIds.Count != 0)
            tasks.Add(Get(
                new() { TransactionId = new() { In = transactionIds } },
                new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit }));

        if (depositIds.Count != 0)
            tasks.Add(Get(
                new() { DepositId = new() { In = depositIds } },
                new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit }));

        await Task.WhenAll(tasks);

        return tasks.SelectMany(x => x.Result);
    }
}
