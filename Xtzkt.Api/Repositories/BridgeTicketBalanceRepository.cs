using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class BridgeTicketBalanceRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"tb.""Id""",             "bigint") },
        { "balance",        (@"tb.""Balance""",        "numeric") },
        { "firstLevel",     (@"tb.""FirstLevel""",     "integer") },
        { "firstTimestamp", (@"tb.""FirstTimestamp""", "timestamptz") },
        { "lastLevel",      (@"tb.""LastLevel""",      "integer") },
        { "lastTimestamp",  (@"tb.""LastTimestamp""",  "timestamptz") },
        { "transfersCount", (@"tb.""TransfersCount""", "integer") },
    };

    async Task<bool> ProcessFilters(BridgeTicketBalanceFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Address?.Hash != null)
            filter.Address.Id += await filter.Address.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Balance?.Gt == 0 && filter.Balance.Ne == null)
        {
            filter.Balance.Gt = null;
            filter.Balance.Ne = 0;
        }

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(BridgeTicketBalanceFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        if (selection == null)
        {
            columns.Add(@"tb.""Id""");
            columns.Add(@"tb.""ChainId""");
            columns.Add(@"tb.""AddressId""");
            columns.Add(@"tb.""Balance""");
            columns.Add(@"tb.""FirstLevel""");
            columns.Add(@"tb.""FirstTimestamp""");
            columns.Add(@"tb.""LastLevel""");
            columns.Add(@"tb.""LastTimestamp""");
            columns.Add(@"tb.""TransfersCount""");
            columns.Add(@"tb.""TicketId"" as ""Ticket_Id""");
            columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash""");
        }
        else
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":             columns.Add(@"tb.""Id"""); break;
                    case "chain":          columns.Add(@"tb.""ChainId"""); break;
                    case "address":        columns.Add(@"tb.""AddressId"""); break;
                    case "balance":        columns.Add(@"tb.""Balance"""); break;
                    case "firstLevel":     columns.Add(@"tb.""FirstLevel"""); break;
                    case "firstTimestamp": columns.Add(@"tb.""FirstTimestamp"""); break;
                    case "lastLevel":      columns.Add(@"tb.""LastLevel"""); break;
                    case "lastTimestamp":  columns.Add(@"tb.""LastTimestamp"""); break;
                    case "transfersCount": columns.Add(@"tb.""TransfersCount"""); break;
                    case "ticket":
                        if (field.Path == null)
                        {
                            columns.Add(@"tb.""TicketId"" as ""Ticket_Id""");
                            columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":          columns.Add(@"tb.""TicketId"" as ""Ticket_Id"""); break;
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
            .From(@"""BridgeTicketBalances""", "tb")
            .InnerJoin(@"""BridgeTickets""", "t", @"""Id""", @"tb.""TicketId""")
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""ChainId""",        filter.Chain?.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TicketId""",       filter.Ticket?.Id)
            .Where(@"t.""WeakHash""",        filter.Ticket?.WeakHash)
            .Where(@"tb.""Balance""",        filter.Balance)
            .Where(@"tb.""FirstLevel""",     filter.FirstLevel)
            .Where(@"tb.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"tb.""LastLevel""",      filter.LastLevel)
            .Where(@"tb.""LastTimestamp""",  filter.LastTimestamp)
            .Where(@"tb.""TransfersCount""", filter.TransfersCount)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(BridgeTicketBalanceFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().OfType<Data.Models.XChain>().Sum(x => x.BridgeTicketBalancesCount);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""BridgeTicketBalances""", "tb");

        if (filter.Ticket?.WeakHash != null)
            sql.InnerJoin(@"""BridgeTickets""", "t", @"""Id""", @"tb.""TicketId""");

        var (query, parameters) = sql
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""ChainId""",        filter.Chain?.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TicketId""",       filter.Ticket?.Id)
            .Where(@"t.""WeakHash""",        filter.Ticket?.WeakHash)
            .Where(@"tb.""Balance""",        filter.Balance)
            .Where(@"tb.""FirstLevel""",     filter.FirstLevel)
            .Where(@"tb.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"tb.""LastLevel""",      filter.LastLevel)
            .Where(@"tb.""LastTimestamp""",  filter.LastTimestamp)
            .Where(@"tb.""TransfersCount""", filter.TransfersCount)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<BridgeTicketBalance>> Get(BridgeTicketBalanceFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new BridgeTicketBalance
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Address = _addressCache.GetInfo((int)row.AddressId),
            Ticket = new BridgeTicketInfo
            {
                Id = row.Ticket_Id,
                WeakHash = row.Ticket_WeakHash,
            },
            Balance = row.Balance,
            FirstLevel = row.FirstLevel,
            FirstTimestamp = row.FirstTimestamp,
            LastLevel = row.LastLevel,
            LastTimestamp = row.LastTimestamp,
            TransfersCount = row.TransfersCount,
        });
    }

    public async Task<object?[][]> Get(BridgeTicketBalanceFilter filter, Pagination pagination, Selection selection)
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
                case "balance":
                    foreach (var row in rows) result[j++][i] = row.Balance;
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
                case "transfersCount":
                    foreach (var row in rows) result[j++][i] = row.TransfersCount;
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
}
