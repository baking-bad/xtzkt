using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class BridgeTicketRepository(
    ChainCache _chainCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
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

    bool ProcessFilters(BridgeTicketFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(BridgeTicketFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        if (selection != null)
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":             columns.Add(@"""Id"""); break;
                    case "chain":          columns.Add(@"""ChainId"""); break;
                    case "weakHash":       columns.Add(@"""WeakHash"""); break;
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
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""BridgeTickets""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""WeakHash""",       filter.WeakHash)
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

    public async Task<long> Count(BridgeTicketFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().OfType<Data.Models.XChain>().Sum(x => x.BridgeTicketsCount);

        if (!ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""BridgeTickets""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""WeakHash""",       filter.WeakHash)
            .Where(@"""FirstLevel""",     filter.FirstLevel)
            .Where(@"""LastLevel""",      filter.LastLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastTimestamp""",  filter.LastTimestamp)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<BridgeTicket>> Get(BridgeTicketFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new BridgeTicket
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            WeakHash = row.WeakHash,
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
        });
    }

    public async Task<object?[][]> Get(BridgeTicketFilter filter, Pagination pagination, Selection selection)
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
                case "weakHash":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[])row.WeakHash);
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
            }
        }

        return result;
    }
}
