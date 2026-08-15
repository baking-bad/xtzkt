using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class TokenBalanceRepository(
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

    async Task<bool> ProcessFilters(TokenBalanceFilter filter)
    {
        filter.Chain?.Id += filter.Chain.ChainId?.ToIdParameter(_chainCache);
        var chainId = filter.Chain?.Id?.Eq;

        if (filter.Address?.Hash != null)
            filter.Address.Id += await filter.Address.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Token?.Contract?.Hash != null)
            filter.Token.Contract.Id += await filter.Token.Contract.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Balance?.Gt == 0 && filter.Balance.Ne == null)
        {
            filter.Balance.Gt = null;
            filter.Balance.Ne = 0;
        }

        if (filter.Token?.Contract?.Id?.Eq != null && filter.Token.TokenId?.Eq != null && filter.Token.Id?.Eq == null)
        {
            await using var db = await _dataSource.OpenConnectionAsync();
            var row = await db.QueryFirstOrDefaultAsync("""
                    SELECT "Id"
                    FROM "Tokens"
                    WHERE "ContractId" = @contractId
                    AND "TokenId" = @tokenId
                    LIMIT 1
                    """, new { contractId = filter.Token.Contract.Id.Eq.Value, tokenId = filter.Token.TokenId.Eq });

            if (row == null)
                return false;

            filter.Token.Contract.Id.Eq = null;
            filter.Token.TokenId.Eq = null;

            filter.Token.Id ??= new();
            filter.Token.Id.Eq = (long)row.Id;
        }

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TokenBalanceFilter filter, Pagination pagination, Selection? selection = null)
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
            columns.Add(@"tb.""Entrypoint""");
            columns.Add(@"tb.""FirstLevel""");
            columns.Add(@"tb.""FirstTimestamp""");
            columns.Add(@"tb.""LastLevel""");
            columns.Add(@"tb.""LastTimestamp""");
            columns.Add(@"tb.""TransfersCount""");
            columns.Add(@"tb.""TokenId"" as ""Token_Id""");
            columns.Add(@"tb.""ContractId"" as ""Token_ContractId""");
            columns.Add(@"t.""TokenId"" as ""Token_TokenId""");
            columns.Add(@"t.""Tags"" as ""Token_Tags""");
            columns.Add(@"t.""TotalSupply"" as ""Token_TotalSupply""");
            columns.Add(@"t.""Name"" as ""Token_Name""");
            columns.Add(@"t.""Symbol"" as ""Token_Symbol""");
            columns.Add(@"t.""Decimals"" as ""Token_Decimals""");
            columns.Add(@"t.""Metadata"" as ""Token_Metadata""");
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
                    case "entrypoint":     columns.Add(@"tb.""Entrypoint"""); break;
                    case "firstLevel":     columns.Add(@"tb.""FirstLevel"""); break;
                    case "firstTimestamp": columns.Add(@"tb.""FirstTimestamp"""); break;
                    case "lastLevel":      columns.Add(@"tb.""LastLevel"""); break;
                    case "lastTimestamp":  columns.Add(@"tb.""LastTimestamp"""); break;
                    case "transfersCount": columns.Add(@"tb.""TransfersCount"""); break;
                    case "token":
                        if (field.Path == null)
                        {
                            columns.Add(@"tb.""TokenId"" as ""Token_Id""");
                            columns.Add(@"tb.""ContractId"" as ""Token_ContractId""");
                            columns.Add(@"t.""TokenId"" as ""Token_TokenId""");
                            columns.Add(@"t.""Tags"" as ""Token_Tags""");
                            columns.Add(@"t.""TotalSupply"" as ""Token_TotalSupply""");
                            columns.Add(@"t.""Name"" as ""Token_Name""");
                            columns.Add(@"t.""Symbol"" as ""Token_Symbol""");
                            columns.Add(@"t.""Decimals"" as ""Token_Decimals""");
                            columns.Add(@"t.""Metadata"" as ""Token_Metadata""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":          columns.Add(@"tb.""TokenId"" as ""Token_Id"""); break;
                                case "contract":    columns.Add(@"tb.""ContractId"" as ""Token_ContractId"""); break;
                                case "tokenId":     columns.Add(@"t.""TokenId"" as ""Token_TokenId"""); break;
                                case "standard":    columns.Add(@"t.""Tags"" as ""Token_Tags"""); break;
                                case "totalSupply": columns.Add(@"t.""TotalSupply"" as ""Token_TotalSupply"""); break;
                                case "name":        columns.Add(@"t.""Name"" as ""Token_Name"""); break;
                                case "symbol":      columns.Add(@"t.""Symbol"" as ""Token_Symbol"""); break;
                                case "decimals":    columns.Add(@"t.""Decimals"" as ""Token_Decimals"""); break;
                                case "metadata":
                                    if (subField.Path == null)
                                    {
                                        columns.Add(@"t.""Metadata"" as ""Token_Metadata""");
                                    }
                                    else
                                    {
                                        field.Column = $"c{counter++}";
                                        columns.Add($@"t.""Metadata"" #> '{{{subField.PathString}}}' as {field.Column}");
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
            .From(@"""TokenBalances""", "tb")
            .InnerJoin(@"""Tokens""", "t", @"""Id""", @"tb.""TokenId""")
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""ChainId""",        filter.Chain?.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TokenId""",        filter.Token?.Id)
            .Where(@"tb.""ContractId""",     filter.Token?.Contract?.Id)
            .Where(@"t.""TokenId""",         filter.Token?.TokenId)
            .Where(@"t.""Tags""",            filter.Token?.Standard)
            .Where(@"t.""Metadata""",        filter.Token?.Metadata)
            .Where(@"tb.""Balance""",        filter.Balance)
            .Where(@"tb.""Entrypoint""",     filter.Entrypoint)
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

    public async Task<long> Count(TokenBalanceFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TokenBalancesCount);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""TokenBalances""", "tb");

        if (filter.Token?.TokenId != null || filter.Token?.Standard != null || filter.Token?.Metadata != null)
            sql.InnerJoin(@"""Tokens""", "t", @"""Id""", @"tb.""TokenId""");

        var (query, parameters) = sql
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""ChainId""",        filter.Chain?.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TokenId""",        filter.Token?.Id)
            .Where(@"tb.""ContractId""",     filter.Token?.Contract?.Id)
            .Where(@"t.""TokenId""",         filter.Token?.TokenId)
            .Where(@"t.""Tags""",            filter.Token?.Standard)
            .Where(@"t.""Metadata""",        filter.Token?.Metadata)
            .Where(@"tb.""Balance""",        filter.Balance)
            .Where(@"tb.""Entrypoint""",     filter.Entrypoint)
            .Where(@"tb.""FirstLevel""",     filter.FirstLevel)
            .Where(@"tb.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"tb.""LastLevel""",      filter.LastLevel)
            .Where(@"tb.""LastTimestamp""",  filter.LastTimestamp)
            .Where(@"tb.""TransfersCount""", filter.TransfersCount)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<TokenBalance>> Get(TokenBalanceFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new TokenBalance
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Address = _addressCache.GetInfo((int)row.AddressId),
            Token = new TokenInfo
            {
                Id = row.Token_Id,
                Contract = _addressCache.GetInfo((int)row.Token_ContractId),
                TokenId = row.Token_TokenId,
                Standard = TokenStandards.ToString((int)row.Token_Tags),
                TotalSupply = row.Token_TotalSupply,
                Name = row.Token_Name,
                Symbol = row.Token_Symbol,
                Decimals = row.Token_Decimals,
                Metadata = row.Token_Metadata,
            },
            Balance = row.Balance,
            Entrypoint = row.Entrypoint,
            FirstLevel = row.FirstLevel,
            FirstTimestamp = row.FirstTimestamp,
            LastLevel = row.LastLevel,
            LastTimestamp = row.LastTimestamp,
            TransfersCount = row.TransfersCount,
        });
    }

    public async Task<object?[][]> Get(TokenBalanceFilter filter, Pagination pagination, Selection selection)
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
                case "entrypoint":
                    foreach (var row in rows) result[j++][i] = Decode.ToUtf8((byte[]?)row.Entrypoint);
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
                case "token":
                    foreach (var row in rows) result[j++][i] = new TokenInfo
                    {
                        Id = row.Token_Id,
                        Contract = _addressCache.GetInfo((int)row.Token_ContractId),
                        TokenId = row.Token_TokenId,
                        Standard = TokenStandards.ToString((int)row.Token_Tags),
                        TotalSupply = row.Token_TotalSupply,
                        Name = row.Token_Name,
                        Symbol = row.Token_Symbol,
                        Decimals = row.Token_Decimals,
                        Metadata = row.Token_Metadata,
                    };
                    break;
                case "token.id":
                    foreach (var row in rows) result[j++][i] = row.Token_Id?.ToString();
                    break;
                case "token.contract":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.Token_ContractId);
                    break;
                case "token.contract.id":
                    foreach (var row in rows) result[j++][i] = row.Token_ContractId;
                    break;
                case "token.contract.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Token_ContractId)).Hash;
                    break;
                case "token.contract.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Token_ContractId)).Type;
                    break;
                case "token.contract.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Token_ContractId)).Alias;
                    break;
                case "token.tokenId":
                    foreach (var row in rows) result[j++][i] = row.Token_TokenId;
                    break;
                case "token.standard":
                    foreach (var row in rows) result[j++][i] = TokenStandards.ToString((int)row.Token_Tags);
                    break;
                case "token.totalSupply":
                    foreach (var row in rows) result[j++][i] = row.Token_TotalSupply;
                    break;
                case "token.name":
                    foreach (var row in rows) result[j++][i] = row.Token_Name;
                    break;
                case "token.symbol":
                    foreach (var row in rows) result[j++][i] = row.Token_Symbol;
                    break;
                case "token.decimals":
                    foreach (var row in rows) result[j++][i] = row.Token_Decimals;
                    break;
                case "token.metadata":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Token_Metadata;
                    break;
                default:
                    if (fields[i].Full.StartsWith("token.metadata."))
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
