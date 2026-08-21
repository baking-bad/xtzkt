using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class TicketRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"""Id""",             "bigint") },
        { "firstLevel",     (@"""FirstLevel""",     "integer") },
        { "firstTimestamp", (@"""FirstTimestamp""", "timestamptz") },
        { "lastLevel",      (@"""LastLevel""",      "integer") },
        { "lastTimestamp",  (@"""LastTimestamp""",  "timestamptz") },
        { "transfersCount", (@"""TransfersCount""", "integer") },
        { "balancesCount",  (@"""BalancesCount""",  "integer") },
        { "holdersCount",   (@"""HoldersCount""",   "integer") },
    };

    async Task<bool> ProcessFilters(TicketFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Ticketer?.Hash != null)
            filter.Ticketer.Id += await filter.Ticketer.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.FirstMinter?.Hash != null)
            filter.FirstMinter.Id += await filter.FirstMinter.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TicketFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "id":             columns.Add(@"""Id"""); break;
                    case "chain":          columns.Add(@"""ChainId"""); break;
                    case "ticketer":       columns.Add(@"""TicketerId"""); break;
                    case "firstMinter":    columns.Add(@"""FirstMinterId"""); break;
                    case "firstLevel":     columns.Add(@"""FirstLevel"""); break;
                    case "firstTimestamp": columns.Add(@"""FirstTimestamp"""); break;
                    case "lastLevel":      columns.Add(@"""LastLevel"""); break;
                    case "lastTimestamp":  columns.Add(@"""LastTimestamp"""); break;
                    case "transfersCount": columns.Add(@"""TransfersCount"""); break;
                    case "balancesCount":  columns.Add(@"""BalancesCount"""); break;
                    case "holdersCount":   columns.Add(@"""HoldersCount"""); break;
                    case "totalMinted":    columns.Add(@"""TotalMinted"""); break;
                    case "totalBurned":    columns.Add(@"""TotalBurned"""); break;
                    case "totalSupply":    columns.Add(@"""TotalSupply"""); break;
                    case "weakHash":       columns.Add(@"""WeakHash"""); break;
                    case "rawType":        columns.Add(@"""RawType"""); break;
                    case "rawContent":     columns.Add(@"""RawContent"""); break;
                    case "content":
                        if (field.Path == null)
                        {
                            columns.Add(@"""JsonContent""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"""JsonContent"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Tickets""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""TicketerId""",     filter.Ticketer?.Id)
            .Where(@"""FirstMinterId""",  filter.FirstMinter?.Id)
            .Where(@"""WeakHash""",       filter.WeakHash)
            .Where(@"""RawType""",        filter.RawType)
            .Where(@"""RawContent""",     filter.RawContent)
            .Where(@"""JsonContent""",    filter.Content)
            .Where(@"""FirstLevel""",     filter.FirstLevel)
            .Where(@"""LastLevel""",      filter.LastLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastTimestamp""",  filter.LastTimestamp)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(TicketFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TicketsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Tickets""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""TicketerId""",     filter.Ticketer?.Id)
            .Where(@"""FirstMinterId""",  filter.FirstMinter?.Id)
            .Where(@"""WeakHash""",       filter.WeakHash)
            .Where(@"""RawType""",        filter.RawType)
            .Where(@"""RawContent""",     filter.RawContent)
            .Where(@"""JsonContent""",    filter.Content)
            .Where(@"""FirstLevel""",     filter.FirstLevel)
            .Where(@"""LastLevel""",      filter.LastLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastTimestamp""",  filter.LastTimestamp)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Ticket>> Get(TicketFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new Ticket
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Ticketer = _addressCache.GetInfo((int)row.TicketerId),
            FirstMinter = _addressCache.GetInfo((int)row.FirstMinterId),
            FirstLevel = row.FirstLevel,
            FirstTimestamp = row.FirstTimestamp,
            LastLevel = row.LastLevel,
            LastTimestamp = row.LastTimestamp,
            TransfersCount = row.TransfersCount,
            BalancesCount = row.BalancesCount,
            HoldersCount = row.HoldersCount,
            TotalMinted = row.TotalMinted,
            TotalBurned = row.TotalBurned,
            TotalSupply = row.TotalSupply,
            WeakHash = row.WeakHash,
            RawType = Micheline.FromBytes((byte[])row.RawType),
            RawContent = Micheline.FromBytes((byte[])row.RawContent),
            Content = row.JsonContent,
        });
    }

    public async Task<object?[][]> Get(TicketFilter filter, Pagination pagination, Selection selection)
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
                case "ticketer":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.TicketerId);
                    break;
                case "ticketer.id":
                    foreach (var row in rows) result[j++][i] = row.TicketerId;
                    break;
                case "ticketer.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Hash;
                    break;
                case "ticketer.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Type;
                    break;
                case "ticketer.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Alias;
                    break;
                case "firstMinter":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.FirstMinterId);
                    break;
                case "firstMinter.id":
                    foreach (var row in rows) result[j++][i] = row.FirstMinterId;
                    break;
                case "firstMinter.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Hash;
                    break;
                case "firstMinter.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Type;
                    break;
                case "firstMinter.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Alias;
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
                case "balancesCount":
                    foreach (var row in rows) result[j++][i] = row.BalancesCount;
                    break;
                case "holdersCount":
                    foreach (var row in rows) result[j++][i] = row.HoldersCount;
                    break;
                case "totalMinted":
                    foreach (var row in rows) result[j++][i] = row.TotalMinted;
                    break;
                case "totalBurned":
                    foreach (var row in rows) result[j++][i] = row.TotalBurned;
                    break;
                case "totalSupply":
                    foreach (var row in rows) result[j++][i] = row.TotalSupply;
                    break;
                case "weakHash":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[])row.WeakHash);
                    break;
                case "rawType":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.RawType);
                    break;
                case "rawContent":
                    foreach (var row in rows) result[j++][i] = Micheline.FromBytes((byte[])row.RawContent);
                    break;
                case "content":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.JsonContent;
                    break;
                default:
                    if (fields[i].Field == "content")
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
