using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;
using Xtzkt.Data.Utils;

namespace Xtzkt.Api.Repositories.Operations;

public class DepositRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    BlockCache _blockCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"o.""Id""",        "bigint") },
        { "timestamp", (@"o.""Timestamp""", "timestamptz") },
    };

    async Task<List<IdRange>?> ProcessFilters(DepositOperationFilter filter, Pagination? pagination = null)
    {
        var chains = _chainCache.Resolve(filter.Chain);
        if (chains.Count == 0)
            return null;

        if (!await _addressCache.Resolve(filter.Receiver, chains))
            return null;

        if (!await _addressCache.Resolve(filter.Proxy, chains))
            return null;

        return await _blockCache.ProcessOpFilters(chains, filter.Id, filter.Level, filter.Timestamp, pagination);
    }

    async Task<IEnumerable<dynamic>> Query(DepositOperationFilter filter, Pagination pagination, Selection? selection = null)
    {
        pagination.Reduce(SortSpec);

        if (await ProcessFilters(filter, pagination) is not List<IdRange> ranges)
            return [];

        var columns = new HashSet<string>();
        var counter = 0;
        if (selection == null)
        {
            columns.Add("o.*");
            columns.Add(@"tk.""Id"" as ""Token_Id""");
            columns.Add(@"tk.""ContractId"" as ""Token_ContractId""");
            columns.Add(@"tk.""TokenId"" as ""Token_TokenId""");
            columns.Add(@"tk.""Tags"" as ""Token_Tags""");
            columns.Add(@"tk.""TotalSupply"" as ""Token_TotalSupply""");
            columns.Add(@"tk.""Name"" as ""Token_Name""");
            columns.Add(@"tk.""Symbol"" as ""Token_Symbol""");
            columns.Add(@"tk.""Decimals"" as ""Token_Decimals""");
            columns.Add(@"tk.""Metadata"" as ""Token_Metadata""");
        }
        else
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "runtime":        columns.Add(@"o.""Runtime"""); break;
                    case "id":             columns.Add(@"o.""Id"""); break;
                    case "chain":          columns.Add(@"o.""ChainId"""); break;
                    case "level":          columns.Add(@"o.""Level"""); break;
                    case "timestamp":      columns.Add(@"o.""Timestamp"""); break;
                    case "hash":           columns.Add(@"o.""Hash"""); columns.Add(@"o.""Runtime"""); break;
                    case "status":         columns.Add(@"o.""Status"""); break;
                    case "inboxLevel":     columns.Add(@"o.""InboxLevel"""); break;
                    case "inboxMessageId": columns.Add(@"o.""InboxMessageId"""); break;
                    case "receiver":       columns.Add(@"o.""ReceiverId"""); break;
                    case "type":           columns.Add(@"o.""Type"""); break;
                    case "amount":         columns.Add(@"o.""Amount"""); columns.Add(@"o.""Amount18"""); columns.Add(@"o.""Runtime"""); break;
                    case "ticketHash":     columns.Add(@"o.""TicketHash"""); break;
                    case "token":
                        if (field.Path == null)
                        {
                            columns.Add(@"tk.""Id"" as ""Token_Id""");
                            columns.Add(@"tk.""ContractId"" as ""Token_ContractId""");
                            columns.Add(@"tk.""TokenId"" as ""Token_TokenId""");
                            columns.Add(@"tk.""Tags"" as ""Token_Tags""");
                            columns.Add(@"tk.""TotalSupply"" as ""Token_TotalSupply""");
                            columns.Add(@"tk.""Name"" as ""Token_Name""");
                            columns.Add(@"tk.""Symbol"" as ""Token_Symbol""");
                            columns.Add(@"tk.""Decimals"" as ""Token_Decimals""");
                            columns.Add(@"tk.""Metadata"" as ""Token_Metadata""");
                        }
                        else
                        {
                            var subField = field.SubField()!;
                            switch (subField.Field)
                            {
                                case "id":          columns.Add(@"tk.""Id"" as ""Token_Id"""); break;
                                case "contract":    columns.Add(@"tk.""ContractId"" as ""Token_ContractId"""); break;
                                case "tokenId":     columns.Add(@"tk.""TokenId"" as ""Token_TokenId"""); break;
                                case "standard":    columns.Add(@"tk.""Tags"" as ""Token_Tags"""); break;
                                case "totalSupply": columns.Add(@"tk.""TotalSupply"" as ""Token_TotalSupply"""); break;
                                case "name":        columns.Add(@"tk.""Name"" as ""Token_Name"""); break;
                                case "symbol":      columns.Add(@"tk.""Symbol"" as ""Token_Symbol"""); break;
                                case "decimals":    columns.Add(@"tk.""Decimals"" as ""Token_Decimals"""); break;
                                case "metadata":
                                    if (subField.Path == null)
                                    {
                                        columns.Add(@"tk.""Metadata"" as ""Token_Metadata""");
                                    }
                                    else
                                    {
                                        field.Column = $"c{counter++}";
                                        columns.Add($@"tk.""Metadata"" #> '{{{subField.PathString}}}' as {field.Column}");
                                    }
                                    break;
                                default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Full} doesn't exist");
                            }
                        }
                        break;
                    case "proxy":          columns.Add(@"o.""ProxyId"""); break;
                    case "depositId":      columns.Add(@"o.""DepositId"""); break;
                    case "claimTransactionId": columns.Add(@"o.""ClaimTransactionId"""); break;
                    case "gasUsed":        columns.Add(@"o.""GasUsed"""); break;
                    case "logsCount":      columns.Add(@"o.""LogsCount"""); break;
                    case "bridgeTicketTransfers": columns.Add(@"o.""BridgeTicketTransfers"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""DepositOps""", "o")
            .LeftJoin(@"""TicketBindings""", "bn", @"""TicketHash""", @"o.""TicketHash""")
            .LeftJoin(@"""Tokens""", "tk", @"""Id""", @"bn.""TokenId""")
            .Where(filter.Or)
            .Where(@"o.""Id""",                   ranges)
            .Where(@"o.""Id""",                   filter.Id)
            .Where(@"o.""Level""",                filter.Level)
            .Where(@"o.""Timestamp""",            filter.Timestamp)
            .Where(@"o.""Hash""",                 filter.Hash)
            .Where(@"o.""Status""",               filter.Status)
            .Where(@"o.""InboxLevel""",           filter.InboxLevel)
            .Where(@"o.""InboxMessageId""",       filter.InboxMessageId)
            .Where(@"o.""ReceiverId""",           filter.Receiver?.Id)
            .Where(@"o.""ProxyId""",              filter.Proxy?.Id)
            .Where(@"o.""Type""",                 filter.Type)
            .Where(@"o.""TicketHash""",           filter.TicketHash)
            .Where(@"o.""DepositId""",            filter.DepositId)
            .Where(@"o.""ClaimTransactionId""",   filter.ClaimTransactionId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(DepositOperationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().OfType<Data.Models.XChain>().Sum(x => x.DepositOpsCount);

        if (await ProcessFilters(filter) is not List<IdRange> ranges)
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""DepositOps""")
            .Where(@"""Id""",                   ranges)
            .Where(@"""Id""",                   filter.Id)
            .Where(@"""Level""",                filter.Level)
            .Where(@"""Timestamp""",            filter.Timestamp)
            .Where(@"""Hash""",                 filter.Hash)
            .Where(@"""Status""",               filter.Status)
            .Where(@"""InboxLevel""",           filter.InboxLevel)
            .Where(@"""InboxMessageId""",       filter.InboxMessageId)
            .Where(@"""ReceiverId""",           filter.Receiver?.Id)
            .Where(@"""ProxyId""",              filter.Proxy?.Id)
            .Where(@"""Type""",                 filter.Type)
            .Where(@"""TicketHash""",           filter.TicketHash)
            .Where(@"""DepositId""",            filter.DepositId)
            .Where(@"""ClaimTransactionId""",   filter.ClaimTransactionId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<DepositOperation>> Get(DepositOperationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, DepositOperation>(row =>
        {
            return (Data.Models.Runtime)(int)row.Runtime switch
            {
                Data.Models.Runtime.Michelson => new XMichelsonDepositOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = Hashes.FormatMichelsonOperationHash(row.Hash),
                    Status = OperationStatuses.ToString((int)row.Status),
                    InboxLevel = row.InboxLevel,
                    InboxMessageId = row.InboxMessageId,
                    Receiver = _addressCache.GetInfo((int)row.ReceiverId),
                    Type = DepositTypes.ToString((int)row.Type),
                    GasUsed = row.GasUsed,
                    Amount = row.Amount,
                },
                Data.Models.Runtime.Evm => new XEvmDepositOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = Hashes.FormatEvmOperationHash(row.Hash),
                    Status = OperationStatuses.ToString((int)row.Status),
                    InboxLevel = row.InboxLevel,
                    InboxMessageId = row.InboxMessageId,
                    Receiver = _addressCache.GetInfo((int)row.ReceiverId),
                    Type = DepositTypes.ToString((int)row.Type),
                    Amount = row.Amount18,
                    TicketHash = row.TicketHash,
                    Token = row.Token_Id == null ? null : new TokenInfo
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
                    Proxy = _addressCache.GetInfo((int?)row.ProxyId),
                    DepositId = row.DepositId,
                    ClaimTransactionId = row.ClaimTransactionId,
                    GasUsed = row.GasUsed,
                    LogsCount = row.LogsCount,
                    BridgeTicketTransfers = row.BridgeTicketTransfers,
                },
                _ => throw new InvalidOperationException("Failed to read DepositOperation")
            };
        });
    }

    public async Task<object?[][]> Get(DepositOperationFilter filter, Pagination pagination, Selection selection)
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
                case "runtime":
                    foreach (var row in rows) result[j++][i] = Runtimes.ToString((int)row.Runtime);
                    break;
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
                case "hash":
                    foreach (var row in rows) result[j++][i] = Hashes.FormatOperationHash(row.Hash, (Data.Models.Runtime)(int)row.Runtime);
                    break;
                case "status":
                    foreach (var row in rows) result[j++][i] = OperationStatuses.ToString((int)row.Status);
                    break;
                case "inboxLevel":
                    foreach (var row in rows) result[j++][i] = row.InboxLevel;
                    break;
                case "inboxMessageId":
                    foreach (var row in rows) result[j++][i] = row.InboxMessageId;
                    break;
                case "receiver":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.ReceiverId);
                    break;
                case "receiver.id":
                    foreach (var row in rows) result[j++][i] = row.ReceiverId;
                    break;
                case "receiver.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Hash;
                    break;
                case "receiver.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Type;
                    break;
                case "receiver.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Domain;
                    break;
                case "receiver.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Profile;
                    break;
                case "receiver.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Owner;
                    break;
                case "receiver.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Owner?.Id;
                    break;
                case "receiver.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Owner?.Hash;
                    break;
                case "receiver.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Owner?.Type;
                    break;
                case "receiver.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Owner?.Domain;
                    break;
                case "receiver.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.ReceiverId)).Owner?.Profile;
                    break;
                case "type":
                    foreach (var row in rows) result[j++][i] = DepositTypes.ToString((int)row.Type);
                    break;
                case "amount":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Runtime)(int)row.Runtime switch
                    {
                        Data.Models.Runtime.Michelson => row.Amount,
                        Data.Models.Runtime.Evm => row.Amount18,
                        _ => null
                    };
                    break;
                case "ticketHash":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[]?)row.TicketHash);
                    break;
                case "token":
                    foreach (var row in rows) result[j++][i] = row.Token_Id == null ? null : new TokenInfo
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
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.Token_ContractId);
                    break;
                case "token.contract.id":
                    foreach (var row in rows) result[j++][i] = row.Token_ContractId;
                    break;
                case "token.contract.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Hash;
                    break;
                case "token.contract.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Type;
                    break;
                case "token.contract.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Domain;
                    break;
                case "token.contract.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Profile;
                    break;
                case "token.contract.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Owner;
                    break;
                case "token.contract.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Owner?.Id;
                    break;
                case "token.contract.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Owner?.Hash;
                    break;
                case "token.contract.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Owner?.Type;
                    break;
                case "token.contract.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Owner?.Domain;
                    break;
                case "token.contract.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.Token_ContractId))?.Owner?.Profile;
                    break;
                case "token.tokenId":
                    foreach (var row in rows) result[j++][i] = row.Token_TokenId;
                    break;
                case "token.standard":
                    foreach (var row in rows) result[j++][i] = row.Token_Tags == null ? null : TokenStandards.ToString((int)row.Token_Tags);
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
                case "proxy":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.ProxyId);
                    break;
                case "proxy.id":
                    foreach (var row in rows) result[j++][i] = row.ProxyId;
                    break;
                case "proxy.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Hash;
                    break;
                case "proxy.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Type;
                    break;
                case "proxy.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Domain;
                    break;
                case "proxy.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Profile;
                    break;
                case "proxy.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Owner;
                    break;
                case "proxy.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Owner?.Id;
                    break;
                case "proxy.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Owner?.Hash;
                    break;
                case "proxy.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Owner?.Type;
                    break;
                case "proxy.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Owner?.Domain;
                    break;
                case "proxy.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ProxyId))?.Owner?.Profile;
                    break;
                case "depositId":
                    foreach (var row in rows) result[j++][i] = row.DepositId;
                    break;
                case "claimTransactionId":
                    foreach (var row in rows) result[j++][i] = row.ClaimTransactionId?.ToString();
                    break;
                case "gasUsed":
                    foreach (var row in rows) result[j++][i] = row.GasUsed;
                    break;
                case "logsCount":
                    foreach (var row in rows) result[j++][i] = row.LogsCount;
                    break;
                case "bridgeTicketTransfers":
                    foreach (var row in rows) result[j++][i] = row.BridgeTicketTransfers;
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
        DateTimeRangeParameter? timestamp,
        ActivityPagination pagination)
    {
        var or = new OrParameterBuilder(pagination.Limit);

        foreach (var address in addresses)
        {
            var count = address switch
            {
                Data.Models.XAddress a => a.DepositOpsCount,
                _ => 0,
            };
            if (count == 0)
                continue;

            if ((roles & ActivityRole.Target) != 0)
                or.Add(@"o.""ReceiverId""", address.Id, count);

            if ((roles & ActivityRole.Mention) != 0 && address is Data.Models.XEvmAddress)
                or.Add(@"o.""ProxyId""", address.Id, count);
        }

        if (or.IsEmpty)
            return [];

        return await Get(
            new() { Or = or.Build(), Chain = chain, Timestamp = timestamp },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }

    public async Task<IEnumerable<IActivity>> Activity(int level, Data.Models.Chain chain, ActivityPagination pagination)
    {
        return await Get(
            new() { Level = level, Chain = chain },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }

    public async Task<IEnumerable<IOpgActivity>> Activity(OperationHashEqParameter hash, ChainInfoParameter? chain, ActivityPagination pagination)
    {
        return await Get(
            new() { Hash = hash.ToOperationHashParameter(), Chain = chain },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }
}
