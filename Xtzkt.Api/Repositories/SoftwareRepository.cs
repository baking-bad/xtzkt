using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class SoftwareRepository(ChainCache _chainCache, NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",         (@"""Id""",         "integer") },
        { "firstLevel", (@"""FirstLevel""", "integer") },
        { "lastLevel",  (@"""LastLevel""",  "integer") },
    };

    bool ProcessFilters(SoftwareFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        return filter.Chain.Id!.Eq != -1;
    }

    async Task<IEnumerable<dynamic>> Query(SoftwareFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "id":          columns.Add(@"""Id"""); break;
                    case "chain":       columns.Add(@"""ChainId"""); break;
                    case "shortHash":   columns.Add(@"""ShortHash"""); break;
                    case "firstLevel":  columns.Add(@"""FirstLevel"""); break;
                    case "lastLevel":   columns.Add(@"""LastLevel"""); break;
                    case "blocksCount": columns.Add(@"""BlocksCount"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Software""")
            .Where(@"""Id""",         filter.Id)
            .Where(@"""ChainId""",    filter.Chain?.Id)
            .Where(@"""ShortHash""",  filter.ShortHash)
            .Where(@"""FirstLevel""", filter.FirstLevel)
            .Where(@"""LastLevel""",  filter.LastLevel)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(SoftwareFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().OfType<Data.Models.L1Chain>().Sum(x => x.SoftwareCounter);

        if (!ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Software""")
            .Where(@"""Id""",         filter.Id)
            .Where(@"""ChainId""",    filter.Chain?.Id)
            .Where(@"""ShortHash""",  filter.ShortHash)
            .Where(@"""FirstLevel""", filter.FirstLevel)
            .Where(@"""LastLevel""",  filter.LastLevel)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Software>> Get(SoftwareFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new Software
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            ShortHash = row.ShortHash,
            FirstLevel = row.FirstLevel,
            LastLevel = row.LastLevel,
            BlocksCount = row.BlocksCount,
        });
    }

    public async Task<object?[][]> Get(SoftwareFilter filter, Pagination pagination, Selection selection)
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
                    foreach (var row in rows) result[j++][i] = row.Id;
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
                case "shortHash":
                    foreach (var row in rows) result[j++][i] = row.ShortHash;
                    break;
                case "firstLevel":
                    foreach (var row in rows) result[j++][i] = row.FirstLevel;
                    break;
                case "lastLevel":
                    foreach (var row in rows) result[j++][i] = row.LastLevel;
                    break;
                case "blocksCount":
                    foreach (var row in rows) result[j++][i] = row.BlocksCount;
                    break;
            }
        }

        return result;
    }
}
