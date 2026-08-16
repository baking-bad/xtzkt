using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class TokenRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"""Id""",             "bigint") },
        { "tokenId",        (@"""TokenId""",        "numeric") },
        { "firstLevel",     (@"""FirstLevel""",     "integer") },
        { "firstTimestamp", (@"""FirstTimestamp""", "timestamptz") },
        { "lastLevel",      (@"""LastLevel""",      "integer") },
        { "lastTimestamp",  (@"""LastTimestamp""",  "timestamptz") },
        { "transfersCount", (@"""TransfersCount""", "integer") },
        { "balancesCount",  (@"""BalancesCount""",  "integer") },
        { "holdersCount",   (@"""HoldersCount""",   "integer") },
    };

    async Task<bool> ProcessFilters(TokenFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Contract?.Hash != null)
            filter.Contract.Id += await filter.Contract.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TokenFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "id":                columns.Add(@"""Id"""); break;
                    case "chain":             columns.Add(@"""ChainId"""); break;
                    case "contract":          columns.Add(@"""ContractId"""); break;
                    case "tokenId":           columns.Add(@"""TokenId"""); break;
                    case "standard":          columns.Add(@"""Tags"""); break;
                    case "firstMinter":       columns.Add(@"""FirstMinterId"""); break;
                    case "firstLevel":        columns.Add(@"""FirstLevel"""); break;
                    case "firstTimestamp":    columns.Add(@"""FirstTimestamp"""); break;
                    case "lastLevel":         columns.Add(@"""LastLevel"""); break;
                    case "lastTimestamp":     columns.Add(@"""LastTimestamp"""); break;
                    case "transfersCount":    columns.Add(@"""TransfersCount"""); break;
                    case "balancesCount":     columns.Add(@"""BalancesCount"""); break;
                    case "holdersCount":      columns.Add(@"""HoldersCount"""); break;
                    case "totalMinted":       columns.Add(@"""TotalMinted"""); break;
                    case "totalBurned":       columns.Add(@"""TotalBurned"""); break;
                    case "totalSupply":       columns.Add(@"""TotalSupply"""); break;
                    case "name":              columns.Add(@"""Name"""); break;
                    case "symbol":            columns.Add(@"""Symbol"""); break;
                    case "decimals":          columns.Add(@"""Decimals"""); break;
                    case "metadataStatus":    columns.Add(@"""MetadataStatus"""); break;
                    case "metadataLink":      columns.Add(@"""MetadataLink"""); break;
                    case "metadataSyncedAt":  columns.Add(@"""MetadataSyncedAt"""); break;
                    case "metadata":
                        if (field.Path == null)
                        {
                            columns.Add(@"""Metadata""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"""Metadata"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Tokens""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""ContractId""",     filter.Contract?.Id)
            .Where(@"""TokenId""",        filter.TokenId)
            .Where(@"""Tags""",           filter.Standard)
            .Where(@"""FirstLevel""",     filter.FirstLevel)
            .Where(@"""LastLevel""",      filter.LastLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastTimestamp""",  filter.LastTimestamp)
            .Where(@"""Metadata""",       filter.Metadata)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(TokenFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TokensCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Tokens""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""ContractId""",     filter.Contract?.Id)
            .Where(@"""TokenId""",        filter.TokenId)
            .Where(@"""Tags""",           filter.Standard)
            .Where(@"""FirstLevel""",     filter.FirstLevel)
            .Where(@"""LastLevel""",      filter.LastLevel)
            .Where(@"""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"""LastTimestamp""",  filter.LastTimestamp)
            .Where(@"""Metadata""",       filter.Metadata)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Token>> Get(TokenFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new Token
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Contract = _addressCache.GetInfo((int)row.ContractId),
            TokenId = row.TokenId,
            Standard = TokenStandards.ToString((int)row.Tags),
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
            Name = row.Name,
            Symbol = row.Symbol,
            Decimals = row.Decimals,
            Metadata = row.Metadata,
            MetadataStatus = TokenMetadataStatuses.ToString((int)row.MetadataStatus),
            MetadataLink = row.MetadataLink,
            MetadataSyncedAt = row.MetadataSyncedAt,
        });
    }

    public async Task<object?[][]> Get(TokenFilter filter, Pagination pagination, Selection selection)
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
                case "contract":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.ContractId);
                    break;
                case "contract.id":
                    foreach (var row in rows) result[j++][i] = row.ContractId;
                    break;
                case "contract.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ContractId)).Hash;
                    break;
                case "contract.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ContractId)).Type;
                    break;
                case "contract.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ContractId)).Alias;
                    break;
                case "tokenId":
                    foreach (var row in rows) result[j++][i] = row.TokenId;
                    break;
                case "standard":
                    foreach (var row in rows) result[j++][i] = TokenStandards.ToString((int)row.Tags);
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
                case "name":
                    foreach (var row in rows) result[j++][i] = row.Name;
                    break;
                case "symbol":
                    foreach (var row in rows) result[j++][i] = row.Symbol;
                    break;
                case "decimals":
                    foreach (var row in rows) result[j++][i] = row.Decimals;
                    break;
                case "metadataStatus":
                    foreach (var row in rows) result[j++][i] = TokenMetadataStatuses.ToString((int)row.MetadataStatus);
                    break;
                case "metadataLink":
                    foreach (var row in rows) result[j++][i] = row.MetadataLink;
                    break;
                case "metadataSyncedAt":
                    foreach (var row in rows) result[j++][i] = row.MetadataSyncedAt;
                    break;
                case "metadata":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Metadata;
                    break;
                default:
                    if (fields[i].Field == "metadata")
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
