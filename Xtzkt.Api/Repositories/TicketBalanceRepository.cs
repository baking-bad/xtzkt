using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class TicketBalanceRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"tb.""Id""",             "bigint") },
        { "balance",        (@"tb.""Balance""",        "numeric") },
        { "transfersCount", (@"tb.""TransfersCount""", "integer") },
        { "firstTimestamp", (@"tb.""FirstTimestamp""", "timestamptz") },
        { "lastTimestamp",  (@"tb.""LastTimestamp""",  "timestamptz") },
    };

    async Task<bool> ProcessFilters(TicketBalanceFilter filter, Pagination? pagination = null)
    {
        #region replace chain filter
        var chains = _chainCache.Resolve(filter.Chain);
        if (chains.Count == 0)
            return false;

        if (chains.Count != _chainCache.Count())
        {
            var idRange = _chainCache.GetId64Range(chains);
            if (!Int64Parameter.TryMerge(filter.Id, idRange, out var id))
                return false;
            filter.Id = id;

            if (filter.FirstTimestamp != null || pagination?.SortingBy("firstTimestamp") == true)
            {
                var timestampRange = _chainCache.GetTimestampRange(chains);
                if (!DateTimeParameter.TryMerge(filter.FirstTimestamp, timestampRange, out var firstTimestamp))
                    return false;
                filter.FirstTimestamp = firstTimestamp;
            }

            if (filter.LastTimestamp != null || pagination?.SortingBy("lastTimestamp") == true)
            {
                var timestampRange = _chainCache.GetTimestampRange(chains);
                if (!DateTimeParameter.TryMerge(filter.LastTimestamp, timestampRange, out var lastTimestamp))
                    return false;
                filter.LastTimestamp = lastTimestamp;
            }
        }
        #endregion

        if (!await _addressCache.Resolve(filter.Address, chains))
            return false;

        if (!await _addressCache.Resolve(filter.Ticket?.Ticketer, chains))
            return false;

        if (filter.Balance?.Gt == 0 && filter.Balance.Ne == null)
        {
            filter.Balance.Gt = null;
            filter.Balance.Ne = 0;
        }

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TicketBalanceFilter filter, Pagination pagination, Selection? selection = null)
    {
        pagination.Reduce(SortSpec);

        if (!await ProcessFilters(filter, pagination))
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
            columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash""");
            columns.Add(@"tk.""Id"" as ""Ticket_Token_Id""");
            columns.Add(@"tk.""ContractId"" as ""Ticket_Token_ContractId""");
            columns.Add(@"tk.""TokenId"" as ""Ticket_Token_TokenId""");
            columns.Add(@"tk.""Tags"" as ""Ticket_Token_Tags""");
            columns.Add(@"tk.""TotalSupply"" as ""Ticket_Token_TotalSupply""");
            columns.Add(@"tk.""Name"" as ""Ticket_Token_Name""");
            columns.Add(@"tk.""Symbol"" as ""Ticket_Token_Symbol""");
            columns.Add(@"tk.""Decimals"" as ""Ticket_Token_Decimals""");
            columns.Add(@"tk.""Metadata"" as ""Ticket_Token_Metadata""");
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
                            columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash""");
                            columns.Add(@"tk.""Id"" as ""Ticket_Token_Id""");
                            columns.Add(@"tk.""ContractId"" as ""Ticket_Token_ContractId""");
                            columns.Add(@"tk.""TokenId"" as ""Ticket_Token_TokenId""");
                            columns.Add(@"tk.""Tags"" as ""Ticket_Token_Tags""");
                            columns.Add(@"tk.""TotalSupply"" as ""Ticket_Token_TotalSupply""");
                            columns.Add(@"tk.""Name"" as ""Ticket_Token_Name""");
                            columns.Add(@"tk.""Symbol"" as ""Ticket_Token_Symbol""");
                            columns.Add(@"tk.""Decimals"" as ""Ticket_Token_Decimals""");
                            columns.Add(@"tk.""Metadata"" as ""Ticket_Token_Metadata""");
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
                                case "weakHash":    columns.Add(@"t.""WeakHash"" as ""Ticket_WeakHash"""); break;
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
                                case "token":
                                    if (subField.Path == null)
                                    {
                                        columns.Add(@"tk.""Id"" as ""Ticket_Token_Id""");
                                        columns.Add(@"tk.""ContractId"" as ""Ticket_Token_ContractId""");
                                        columns.Add(@"tk.""TokenId"" as ""Ticket_Token_TokenId""");
                                        columns.Add(@"tk.""Tags"" as ""Ticket_Token_Tags""");
                                        columns.Add(@"tk.""TotalSupply"" as ""Ticket_Token_TotalSupply""");
                                        columns.Add(@"tk.""Name"" as ""Ticket_Token_Name""");
                                        columns.Add(@"tk.""Symbol"" as ""Ticket_Token_Symbol""");
                                        columns.Add(@"tk.""Decimals"" as ""Ticket_Token_Decimals""");
                                        columns.Add(@"tk.""Metadata"" as ""Ticket_Token_Metadata""");
                                    }
                                    else
                                    {
                                        var tokenField = subField.SubField()!;
                                        switch (tokenField.Field)
                                        {
                                            case "id":          columns.Add(@"tk.""Id"" as ""Ticket_Token_Id"""); break;
                                            case "contract":    columns.Add(@"tk.""ContractId"" as ""Ticket_Token_ContractId"""); break;
                                            case "tokenId":     columns.Add(@"tk.""TokenId"" as ""Ticket_Token_TokenId"""); break;
                                            case "standard":    columns.Add(@"tk.""Tags"" as ""Ticket_Token_Tags"""); break;
                                            case "totalSupply": columns.Add(@"tk.""TotalSupply"" as ""Ticket_Token_TotalSupply"""); break;
                                            case "name":        columns.Add(@"tk.""Name"" as ""Ticket_Token_Name"""); break;
                                            case "symbol":      columns.Add(@"tk.""Symbol"" as ""Ticket_Token_Symbol"""); break;
                                            case "decimals":    columns.Add(@"tk.""Decimals"" as ""Ticket_Token_Decimals"""); break;
                                            case "metadata":
                                                if (tokenField.Path == null)
                                                {
                                                    columns.Add(@"tk.""Metadata"" as ""Ticket_Token_Metadata""");
                                                }
                                                else
                                                {
                                                    field.Column = $"c{counter++}";
                                                    columns.Add($@"tk.""Metadata"" #> '{{{tokenField.PathString}}}' as {field.Column}");
                                                }
                                                break;
                                            default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Full} doesn't exist");
                                        }
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
            .LeftJoin(@"""TicketBindings""", "bn", @"""TicketHash""", @"t.""WeakHash""")
            .LeftJoin(@"""Tokens""", "tk", @"""Id""", @"bn.""TokenId""")
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TicketId""",       filter.Ticket?.Id)
            .Where(@"tb.""TicketerId""",     filter.Ticket?.Ticketer?.Id)
            .Where(@"t.""RawType""",         filter.Ticket?.RawType)
            .Where(@"t.""RawContent""",      filter.Ticket?.RawContent)
            .Where(@"t.""JsonContent""",     filter.Ticket?.Content)
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
            filter.Ticket?.WeakHash != null)
            sql.InnerJoin(@"""Tickets""", "t", @"""Id""", @"tb.""TicketId""");

        var (query, parameters) = sql
            .Where(@"tb.""Id""",             filter.Id)
            .Where(@"tb.""AddressId""",      filter.Address?.Id)
            .Where(@"tb.""TicketId""",       filter.Ticket?.Id)
            .Where(@"tb.""TicketerId""",     filter.Ticket?.Ticketer?.Id)
            .Where(@"t.""RawType""",         filter.Ticket?.RawType)
            .Where(@"t.""RawContent""",      filter.Ticket?.RawContent)
            .Where(@"t.""JsonContent""",     filter.Ticket?.Content)
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
                WeakHash = row.Ticket_WeakHash,
                Token = row.Ticket_Token_Id == null ? null : new TokenInfo
                {
                    Id = row.Ticket_Token_Id,
                    Contract = _addressCache.GetInfo((int)row.Ticket_Token_ContractId),
                    TokenId = row.Ticket_Token_TokenId,
                    Standard = TokenStandards.ToString((int)row.Ticket_Token_Tags),
                    TotalSupply = row.Ticket_Token_TotalSupply,
                    Name = row.Ticket_Token_Name,
                    Symbol = row.Ticket_Token_Symbol,
                    Decimals = row.Ticket_Token_Decimals,
                    Metadata = row.Ticket_Token_Metadata,
                },
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
                case "address.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Domain;
                    break;
                case "address.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Profile;
                    break;
                case "address.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Owner;
                    break;
                case "address.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Owner?.Id;
                    break;
                case "address.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Owner?.Hash;
                    break;
                case "address.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Owner?.Type;
                    break;
                case "address.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Owner?.Domain;
                    break;
                case "address.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AddressId)).Owner?.Profile;
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
                        WeakHash = row.Ticket_WeakHash,
                        Token = row.Ticket_Token_Id == null ? null : new TokenInfo
                        {
                            Id = row.Ticket_Token_Id,
                            Contract = _addressCache.GetInfo((int)row.Ticket_Token_ContractId),
                            TokenId = row.Ticket_Token_TokenId,
                            Standard = TokenStandards.ToString((int)row.Ticket_Token_Tags),
                            TotalSupply = row.Ticket_Token_TotalSupply,
                            Name = row.Ticket_Token_Name,
                            Symbol = row.Ticket_Token_Symbol,
                            Decimals = row.Ticket_Token_Decimals,
                            Metadata = row.Ticket_Token_Metadata,
                        },
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
                case "ticket.ticketer.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Domain;
                    break;
                case "ticket.ticketer.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Profile;
                    break;
                case "ticket.ticketer.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Owner;
                    break;
                case "ticket.ticketer.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Owner?.Id;
                    break;
                case "ticket.ticketer.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Owner?.Hash;
                    break;
                case "ticket.ticketer.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Owner?.Type;
                    break;
                case "ticket.ticketer.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Owner?.Domain;
                    break;
                case "ticket.ticketer.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.Ticket_TicketerId)).Owner?.Profile;
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
                case "ticket.weakHash":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[])row.Ticket_WeakHash);
                    break;
                case "ticket.token":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_Id == null ? null : new TokenInfo
                    {
                        Id = row.Ticket_Token_Id,
                        Contract = _addressCache.GetInfo((int)row.Ticket_Token_ContractId),
                        TokenId = row.Ticket_Token_TokenId,
                        Standard = TokenStandards.ToString((int)row.Ticket_Token_Tags),
                        TotalSupply = row.Ticket_Token_TotalSupply,
                        Name = row.Ticket_Token_Name,
                        Symbol = row.Ticket_Token_Symbol,
                        Decimals = row.Ticket_Token_Decimals,
                        Metadata = row.Ticket_Token_Metadata,
                    };
                    break;
                case "ticket.token.id":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_Id?.ToString();
                    break;
                case "ticket.token.contract":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId);
                    break;
                case "ticket.token.contract.id":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_ContractId;
                    break;
                case "ticket.token.contract.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Hash;
                    break;
                case "ticket.token.contract.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Type;
                    break;
                case "ticket.token.contract.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Domain;
                    break;
                case "ticket.token.contract.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Profile;
                    break;
                case "ticket.token.contract.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Owner;
                    break;
                case "ticket.token.contract.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Owner?.Id;
                    break;
                case "ticket.token.contract.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Owner?.Hash;
                    break;
                case "ticket.token.contract.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Owner?.Type;
                    break;
                case "ticket.token.contract.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Owner?.Domain;
                    break;
                case "ticket.token.contract.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Ticket_Token_ContractId))?.Owner?.Profile;
                    break;
                case "ticket.token.tokenId":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_TokenId;
                    break;
                case "ticket.token.standard":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_Tags == null ? null : TokenStandards.ToString((int)row.Ticket_Token_Tags);
                    break;
                case "ticket.token.totalSupply":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_TotalSupply;
                    break;
                case "ticket.token.name":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_Name;
                    break;
                case "ticket.token.symbol":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_Symbol;
                    break;
                case "ticket.token.decimals":
                    foreach (var row in rows) result[j++][i] = row.Ticket_Token_Decimals;
                    break;
                case "ticket.token.metadata":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Ticket_Token_Metadata;
                    break;
                default:
                    if (fields[i].Full.StartsWith("ticket.content.") || fields[i].Full.StartsWith("ticket.token.metadata."))
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
