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

public class TicketRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    AssetCache _assetCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",             (@"t.""Id""",             "bigint") },
        { "holdersCount",   (@"t.""HoldersCount""",   "integer") },
        { "transfersCount", (@"t.""TransfersCount""", "integer") },
        { "firstTimestamp", (@"t.""FirstTimestamp""", "timestamptz") },
        { "lastTimestamp",  (@"t.""LastTimestamp""",  "timestamptz") },
    };

    async Task<bool> ProcessFilters(TicketFilter filter, Pagination? pagination = null)
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

        if (!await _addressCache.Resolve(filter.Ticketer, chains))
            return false;

        if (!await _addressCache.Resolve(filter.FirstMinter, chains))
            return false;

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TicketFilter filter, Pagination pagination, Selection? selection = null)
    {
        pagination.Reduce(SortSpec);

        if (!await ProcessFilters(filter, pagination))
            return [];

        var columns = new HashSet<string>();
        if (selection == null)
        {
            columns.Add("t.*");
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
            var counter = 0;
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "id":             columns.Add(@"t.""Id"""); break;
                    case "chain":          columns.Add(@"t.""ChainId"""); break;
                    case "ticketer":       columns.Add(@"t.""TicketerId"""); break;
                    case "firstMinter":    columns.Add(@"t.""FirstMinterId"""); break;
                    case "firstLevel":     columns.Add(@"t.""FirstLevel"""); break;
                    case "firstTimestamp": columns.Add(@"t.""FirstTimestamp"""); break;
                    case "lastLevel":      columns.Add(@"t.""LastLevel"""); break;
                    case "lastTimestamp":  columns.Add(@"t.""LastTimestamp"""); break;
                    case "transfersCount": columns.Add(@"t.""TransfersCount"""); break;
                    case "balancesCount":  columns.Add(@"t.""BalancesCount"""); break;
                    case "holdersCount":   columns.Add(@"t.""HoldersCount"""); break;
                    case "totalMinted":    columns.Add(@"t.""TotalMinted"""); break;
                    case "totalBurned":    columns.Add(@"t.""TotalBurned"""); break;
                    case "totalSupply":    columns.Add(@"t.""TotalSupply"""); break;
                    case "weakHash":       columns.Add(@"t.""WeakHash"""); break;
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
                                case "asset":       columns.Add(@"tk.""Id"" as ""Token_Id"""); break;
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
                    case "rawType":        columns.Add(@"t.""RawType"""); break;
                    case "rawContent":     columns.Add(@"t.""RawContent"""); break;
                    case "content":
                        if (field.Path == null)
                        {
                            columns.Add(@"t.""JsonContent""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"t.""JsonContent"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Tickets""", "t")
            .LeftJoin(@"""TicketBindings""", "bn", @"""TicketHash""", @"t.""WeakHash""")
            .LeftJoin(@"""Tokens""", "tk", @"""Id""", @"bn.""TokenId""")
            .Where(@"t.""Id""",             filter.Id)
            .Where(@"t.""TicketerId""",     filter.Ticketer?.Id)
            .Where(@"t.""FirstMinterId""",  filter.FirstMinter?.Id)
            .Where(@"t.""WeakHash""",       filter.WeakHash)
            .Where(@"t.""RawType""",        filter.RawType)
            .Where(@"t.""RawContent""",     filter.RawContent)
            .Where(@"t.""JsonContent""",    filter.Content)
            .Where(@"t.""FirstLevel""",     filter.FirstLevel)
            .Where(@"t.""LastLevel""",      filter.LastLevel)
            .Where(@"t.""FirstTimestamp""", filter.FirstTimestamp)
            .Where(@"t.""LastTimestamp""",  filter.LastTimestamp)
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
                Asset = _assetCache.GetInfo((long)row.Token_Id),
            },
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
                case "ticketer.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Domain;
                    break;
                case "ticketer.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Profile;
                    break;
                case "ticketer.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Owner;
                    break;
                case "ticketer.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Owner?.Id;
                    break;
                case "ticketer.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Owner?.Hash;
                    break;
                case "ticketer.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Owner?.Type;
                    break;
                case "ticketer.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Owner?.Domain;
                    break;
                case "ticketer.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TicketerId)).Owner?.Profile;
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
                case "firstMinter.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Domain;
                    break;
                case "firstMinter.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Profile;
                    break;
                case "firstMinter.owner":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Owner;
                    break;
                case "firstMinter.owner.id":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Owner?.Id;
                    break;
                case "firstMinter.owner.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Owner?.Hash;
                    break;
                case "firstMinter.owner.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Owner?.Type;
                    break;
                case "firstMinter.owner.domain":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Owner?.Domain;
                    break;
                case "firstMinter.owner.profile":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.FirstMinterId)).Owner?.Profile;
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
                        Asset = _assetCache.GetInfo((long)row.Token_Id),
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
                case "token.asset":
                    foreach (var row in rows) result[j++][i] = _assetCache.GetInfo((long?)row.Token_Id);
                    break;
                case "token.asset.id":
                    foreach (var row in rows) result[j++][i] = _assetCache.GetInfo((long?)row.Token_Id)?.Id;
                    break;
                case "token.asset.name":
                    foreach (var row in rows) result[j++][i] = _assetCache.GetInfo((long?)row.Token_Id)?.Name;
                    break;
                case "token.asset.symbol":
                    foreach (var row in rows) result[j++][i] = _assetCache.GetInfo((long?)row.Token_Id)?.Symbol;
                    break;
                case "token.asset.logo":
                    foreach (var row in rows) result[j++][i] = _assetCache.GetInfo((long?)row.Token_Id)?.Logo;
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
                    if (fields[i].Field == "content" || fields[i].Full.StartsWith("token.metadata."))
                        foreach (var row in rows)
                            result[j++][i] = (RawJson?)((row as IDictionary<string, object>)![fields[i].Column!] as string);
                    break;
            }
        }

        return result;
    }
}
