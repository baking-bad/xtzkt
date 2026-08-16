using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class TicketBalanceRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"tb.""Id""",             "bigint") },
        { "balance",        (@"tb.""Balance""",        "numeric") },
        { "firstLevel",     (@"tb.""FirstLevel""",     "integer") },
        { "firstTimestamp", (@"tb.""FirstTimestamp""", "timestamptz") },
        { "lastLevel",      (@"tb.""LastLevel""",      "integer") },
        { "lastTimestamp",  (@"tb.""LastTimestamp""",  "timestamptz") },
        { "transfersCount", (@"tb.""TransfersCount""", "integer") },
    };

    async Task<bool> ProcessFilters(TicketBalanceFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Address?.Hash != null)
            filter.Address.Id += await filter.Address.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Ticket?.Ticketer?.Hash != null)
            filter.Ticket.Ticketer.Id += await filter.Ticket.Ticketer.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Balance?.Gt == 0 && filter.Balance.Ne == null)
        {
            filter.Balance.Gt = null;
            filter.Balance.Ne = 0;
        }

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TicketBalanceFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        var counter = 0;
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
            columns.Add(@"tb.""TicketerId"" as ""Ticket_TicketerId""");
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
                            columns.Add(@"tb.""TicketerId"" as ""Ticket_TicketerId""");
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
                                case "id":          columns.Add(@"tb.""TicketId"" as ""Ticket_Id"""); break;
                                case "ticketer":    columns.Add(@"tb.""TicketerId"" as ""Ticket_TicketerId"""); break;
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
            .From(@"""TicketBalances""", "tb")
            .InnerJoin(@"""Tickets""", "t", @"""Id""", @"tb.""TicketId""")
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""ChainId""",        filter.Chain?.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TicketId""",       filter.Ticket?.Id)
            .Where(@"tb.""TicketerId""",     filter.Ticket?.Ticketer?.Id)
            .Where(@"t.""RawType""",         filter.Ticket?.RawType)
            .Where(@"t.""RawContent""",      filter.Ticket?.RawContent)
            .Where(@"t.""JsonContent""",     filter.Ticket?.Content)
            .Where(@"t.""TypeHash""",        filter.Ticket?.TypeHash)
            .Where(@"t.""ContentHash""",     filter.Ticket?.ContentHash)
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

    public async Task<long> Count(TicketBalanceFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TicketBalancesCount);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""TicketBalances""", "tb");

        if (filter.Ticket?.RawType != null || filter.Ticket?.RawContent != null || filter.Ticket?.Content != null ||
            filter.Ticket?.TypeHash != null || filter.Ticket?.ContentHash != null)
            sql.InnerJoin(@"""Tickets""", "t", @"""Id""", @"tb.""TicketId""");

        var (query, parameters) = sql
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""ChainId""",        filter.Chain?.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TicketId""",       filter.Ticket?.Id)
            .Where(@"tb.""TicketerId""",     filter.Ticket?.Ticketer?.Id)
            .Where(@"t.""RawType""",         filter.Ticket?.RawType)
            .Where(@"t.""RawContent""",      filter.Ticket?.RawContent)
            .Where(@"t.""JsonContent""",     filter.Ticket?.Content)
            .Where(@"t.""TypeHash""",        filter.Ticket?.TypeHash)
            .Where(@"t.""ContentHash""",     filter.Ticket?.ContentHash)
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

    public async Task<IEnumerable<TicketBalance>> Get(TicketBalanceFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new TicketBalance
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Address = _addressCache.GetInfo((int)row.AddressId),
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
            Balance = row.Balance,
            FirstLevel = row.FirstLevel,
            FirstTimestamp = row.FirstTimestamp,
            LastLevel = row.LastLevel,
            LastTimestamp = row.LastTimestamp,
            TransfersCount = row.TransfersCount,
        });
    }

    public async Task<object?[][]> Get(TicketBalanceFilter filter, Pagination pagination, Selection selection)
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
}
