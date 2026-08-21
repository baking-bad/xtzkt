using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class TokenTransferRepository(
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

    async Task<bool> ProcessFilters(TokenTransferFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.From?.Hash != null)
            filter.From.Id += await filter.From.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.To?.Hash != null)
            filter.To.Id += await filter.To.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Token?.Contract?.Hash != null)
            filter.Token.Contract.Id += await filter.Token.Contract.Hash.ToIdParameter(_addressCache, chainId);

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

    async Task<IEnumerable<dynamic>> Query(TokenTransferFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        var counter = 0;
        if (selection == null)
        {
            columns.Add(@"tt.""Id""");
            columns.Add(@"tt.""ChainId""");
            columns.Add(@"tt.""Level""");
            columns.Add(@"tt.""Timestamp""");
            columns.Add(@"tt.""FromId""");
            columns.Add(@"tt.""FromEntrypoint""");
            columns.Add(@"tt.""ToId""");
            columns.Add(@"tt.""ToEntrypoint""");
            columns.Add(@"tt.""Amount""");
            columns.Add(@"tt.""TransactionId""");
            columns.Add(@"tt.""OriginationId""");
            columns.Add(@"tt.""MigrationId""");
            columns.Add(@"tt.""TokenId"" as ""Token_Id""");
            columns.Add(@"tt.""ContractId"" as ""Token_ContractId""");
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
                    case "id":            columns.Add(@"tt.""Id"""); break;
                    case "chain":         columns.Add(@"tt.""ChainId"""); break;
                    case "level":         columns.Add(@"tt.""Level"""); break;
                    case "timestamp":     columns.Add(@"tt.""Timestamp"""); break;
                    case "from":          columns.Add(@"tt.""FromId"""); break;
                    case "fromEntrypoint":columns.Add(@"tt.""FromEntrypoint"""); break;
                    case "to":            columns.Add(@"tt.""ToId"""); break;
                    case "toEntrypoint":  columns.Add(@"tt.""ToEntrypoint"""); break;
                    case "amount":        columns.Add(@"tt.""Amount"""); break;
                    case "transactionId": columns.Add(@"tt.""TransactionId"""); break;
                    case "originationId": columns.Add(@"tt.""OriginationId"""); break;
                    case "migrationId":   columns.Add(@"tt.""MigrationId"""); break;
                    case "token":
                        if (field.Path == null)
                        {
                            columns.Add(@"tt.""TokenId"" as ""Token_Id""");
                            columns.Add(@"tt.""ContractId"" as ""Token_ContractId""");
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
                                case "id":          columns.Add(@"tt.""TokenId"" as ""Token_Id"""); break;
                                case "contract":    columns.Add(@"tt.""ContractId"" as ""Token_ContractId"""); break;
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
            .From(@"""TokenTransfers""", "tt")
            .InnerJoin(@"""Tokens""", "t", @"""Id""", @"tt.""TokenId""")
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
            .Where(@"tt.""TokenId""",        filter.Token?.Id)
            .Where(@"tt.""ContractId""",     filter.Token?.Contract?.Id)
            .Where(@"t.""TokenId""",         filter.Token?.TokenId)
            .Where(@"t.""Tags""",            filter.Token?.Standard)
            .Where(@"t.""Metadata""",        filter.Token?.Metadata)
            .Where(@"tt.""FromId""",         filter.From?.Id)
            .Where(@"tt.""FromEntrypoint""", filter.FromEntrypoint)
            .Where(@"tt.""ToId""",           filter.To?.Id)
            .Where(@"tt.""ToEntrypoint""",   filter.ToEntrypoint)
            .Where(@"tt.""Amount""",         filter.Amount)
            .Where(@"tt.""TransactionId""",  filter.TransactionId)
            .Where(@"tt.""OriginationId""",  filter.OriginationId)
            .Where(@"tt.""MigrationId""",    filter.MigrationId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(TokenTransferFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TokenTransfersCount);

        if (!await ProcessFilters(filter))
            return 0;

        var sql = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""TokenTransfers""", "tt");

        if (filter.Token?.TokenId != null || filter.Token?.Standard != null || filter.Token?.Metadata != null)
            sql.InnerJoin(@"""Tokens""", "t", @"""Id""", @"tt.""TokenId""");

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
            .Where(@"tt.""TokenId""",        filter.Token?.Id)
            .Where(@"tt.""ContractId""",     filter.Token?.Contract?.Id)
            .Where(@"t.""TokenId""",         filter.Token?.TokenId)
            .Where(@"t.""Tags""",            filter.Token?.Standard)
            .Where(@"t.""Metadata""",        filter.Token?.Metadata)
            .Where(@"tt.""FromId""",         filter.From?.Id)
            .Where(@"tt.""FromEntrypoint""", filter.FromEntrypoint)
            .Where(@"tt.""ToId""",           filter.To?.Id)
            .Where(@"tt.""ToEntrypoint""",   filter.ToEntrypoint)
            .Where(@"tt.""Amount""",         filter.Amount)
            .Where(@"tt.""TransactionId""",  filter.TransactionId)
            .Where(@"tt.""OriginationId""",  filter.OriginationId)
            .Where(@"tt.""MigrationId""",    filter.MigrationId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<TokenTransfer>> Get(TokenTransferFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new TokenTransfer
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = row.Level,
            Timestamp = row.Timestamp,
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
            From = _addressCache.GetInfo((int?)row.FromId),
            FromEntrypoint = row.FromEntrypoint,
            To = _addressCache.GetInfo((int?)row.ToId),
            ToEntrypoint = row.ToEntrypoint,
            Amount = row.Amount,
            TransactionId = row.TransactionId,
            OriginationId = row.OriginationId,
            MigrationId = row.MigrationId,
        });
    }

    public async Task<object?[][]> Get(TokenTransferFilter filter, Pagination pagination, Selection selection)
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
                case "fromEntrypoint":
                    foreach (var row in rows) result[j++][i] = Decode.ToUtf8((byte[]?)row.FromEntrypoint);
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
                case "toEntrypoint":
                    foreach (var row in rows) result[j++][i] = Decode.ToUtf8((byte[]?)row.ToEntrypoint);
                    break;
                case "amount":
                    foreach (var row in rows) result[j++][i] = row.Amount;
                    break;
                case "transactionId":
                    foreach (var row in rows) result[j++][i] = row.TransactionId?.ToString();
                    break;
                case "originationId":
                    foreach (var row in rows) result[j++][i] = row.OriginationId?.ToString();
                    break;
                case "migrationId":
                    foreach (var row in rows) result[j++][i] = row.MigrationId?.ToString();
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
            if (address.TokenTransfersCount == 0)
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

    public async Task<IEnumerable<IOpgActivity>> Activity(List<long> transactionIds, List<long> originationIds, CursorPagination pagination)
    {
        var tasks = new List<Task<IEnumerable<TokenTransfer>>>(2);

        if (transactionIds.Count != 0)
            tasks.Add(Get(
                new() { TransactionId = new() { In = transactionIds } },
                new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit }));

        if (originationIds.Count != 0)
            tasks.Add(Get(
                new() { OriginationId = new() { In = originationIds } },
                new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit }));

        await Task.WhenAll(tasks);

        return tasks.SelectMany(x => x.Result);
    }
}