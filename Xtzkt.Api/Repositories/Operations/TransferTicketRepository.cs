using Dapper;
using Netezos.Encoding;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Models.Operations;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories.Operations;

public class TransferTicketRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"""Id""",        "bigint") },
        { "level",     (@"""Level""",     "integer") },
        { "timestamp", (@"""Timestamp""", "timestamptz") },
    };

    async Task<bool> ProcessFilters(TransferTicketOperationFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Sender?.Hash != null)
            filter.Sender.Id += await filter.Sender.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Target?.Hash != null)
            filter.Target.Id += await filter.Target.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Ticketer?.Hash != null)
            filter.Ticketer.Id += await filter.Ticketer.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TransferTicketOperationFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "layer":              columns.Add(@"""Layer"""); break;
                    case "id":                 columns.Add(@"""Id"""); break;
                    case "chain":              columns.Add(@"""ChainId"""); break;
                    case "level":              columns.Add(@"""Level"""); break;
                    case "timestamp":          columns.Add(@"""Timestamp"""); break;
                    case "hash":               columns.Add(@"""Hash"""); break;
                    case "sender":             columns.Add(@"""SenderId"""); break;
                    case "target":             columns.Add(@"""TargetId"""); break;
                    case "ticketer":           columns.Add(@"""TicketerId"""); break;
                    case "amount":             columns.Add(@"""Amount"""); break;
                    case "entrypoint":         columns.Add(@"""Entrypoint"""); break;
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
                    case "contentRaw":         columns.Add(@"""RawContent"""); break;
                    case "typeRaw":            columns.Add(@"""RawType"""); break;
                    case "counter":            columns.Add(@"""Counter"""); break;
                    case "gasLimit":           columns.Add(@"""GasLimit"""); break;
                    case "gasUsed":            columns.Add(@"""GasUsed"""); break;
                    case "storageLimit":       columns.Add(@"""StorageLimit"""); break;
                    case "storageUsed":        columns.Add(@"""StorageUsed"""); break;
                    case "storageFee":         columns.Add(@"""StorageFee"""); break;
                    case "status":             columns.Add(@"""Status"""); break;
                    case "errors":             columns.Add(@"""Errors"""); break;
                    case "ticketTransfers":    columns.Add(@"""TicketTransfers"""); break;
                    case "internalOperations": columns.Add(@"""InternalOperations"""); break;
                    case "bakerFee":           columns.Add(@"""BakerFee"""); break;
                    case "daFee":              columns.Add(@"""DaFee"""); break;
                    case "gasFee":             columns.Add(@"""GasFee"""); break;
                    case "gasRefund":          columns.Add(@"""GasRefund"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""TransferTicketOps""")
            .Where(filter.Or)
            .Where(filter.Anyof, x => x switch
            {
                "sender" => @"""SenderId""",
                "target" => @"""TargetId""",
                "ticketer" => @"""TicketerId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `sender`, `target` and `ticketer` fields only."),
            })
            .Where(@"""Id""",         filter.Id)
            .Where(@"""ChainId""",    filter.Chain?.Id)
            .Where(@"""Level""",      filter.Level)
            .Where(@"""Timestamp""",  filter.Timestamp)
            .Where(@"""Hash""",       filter.Hash, "char(51)")
            .Where(@"""SenderId""",   filter.Sender?.Id)
            .Where(@"""TargetId""",   filter.Target?.Id)
            .Where(@"""TicketerId""", filter.Ticketer?.Id)
            .Where(@"""Amount""",     filter.Amount)
            .Where(@"""Entrypoint""", filter.Entrypoint)
            .Where(@"""Counter""",    filter.Counter)
            .Where(@"""Status""",     filter.Status)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(TransferTicketOperationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TransferTicketOpsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""TransferTicketOps""")
            .Where(filter.Anyof, x => x switch
            {
                "sender" => @"""SenderId""",
                "target" => @"""TargetId""",
                "ticketer" => @"""TicketerId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `sender`, `target` and `ticketer` fields only."),
            })
            .Where(@"""Id""",         filter.Id)
            .Where(@"""ChainId""",    filter.Chain?.Id)
            .Where(@"""Level""",      filter.Level)
            .Where(@"""Timestamp""",  filter.Timestamp)
            .Where(@"""Hash""",       filter.Hash, "char(51)")
            .Where(@"""SenderId""",   filter.Sender?.Id)
            .Where(@"""TargetId""",   filter.Target?.Id)
            .Where(@"""TicketerId""", filter.Ticketer?.Id)
            .Where(@"""Amount""",     filter.Amount)
            .Where(@"""Entrypoint""", filter.Entrypoint)
            .Where(@"""Counter""",    filter.Counter)
            .Where(@"""Status""",     filter.Status)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<TransferTicketOperation>> Get(TransferTicketOperationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, TransferTicketOperation>(row =>
        {
            return (Data.Models.Layer)(int)row.Layer switch
            {
                Data.Models.Layer.L1 => new L1TransferTicketOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    Target = _addressCache.GetInfo((int)row.TargetId),
                    Ticketer = _addressCache.GetInfo((int)row.TicketerId),
                    Amount = row.Amount,
                    Entrypoint = row.Entrypoint,
                    Content = row.JsonContent,
                    ContentRaw = Decode.ToMicheline((byte[]?)row.RawContent),
                    TypeRaw = Decode.ToMicheline((byte[]?)row.RawType),
                    Counter = row.Counter,
                    StorageFee = row.StorageFee,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    TicketTransfers = row.TicketTransfers,
                    InternalOperations = row.InternalOperations,
                    BakerFee = row.BakerFee,
                },
                Data.Models.Layer.TezosX => new XTransferTicketOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    Target = _addressCache.GetInfo((int)row.TargetId),
                    Ticketer = _addressCache.GetInfo((int)row.TicketerId),
                    Amount = row.Amount,
                    Entrypoint = row.Entrypoint,
                    Content = row.JsonContent,
                    ContentRaw = Decode.ToMicheline((byte[]?)row.RawContent),
                    TypeRaw = Decode.ToMicheline((byte[]?)row.RawType),
                    Counter = row.Counter,
                    StorageFee = row.StorageFee,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    TicketTransfers = row.TicketTransfers,
                    InternalOperations = row.InternalOperations,
                    DaFee = row.DaFee,
                    GasFee = row.GasFee,
                    GasRefund = row.GasRefund,
                },
                _ => throw new InvalidOperationException("Failed to read TransferTicketOperation")
            };
        });
    }

    public async Task<object?[][]> Get(TransferTicketOperationFilter filter, Pagination pagination, Selection selection)
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
                case "layer":
                    foreach (var row in rows) result[j++][i] = Layers.ToString((int)row.Layer);
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
                    foreach (var row in rows) result[j++][i] = row.Hash;
                    break;
                case "sender":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.SenderId);
                    break;
                case "sender.id":
                    foreach (var row in rows) result[j++][i] = row.SenderId;
                    break;
                case "sender.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.SenderId)).Hash;
                    break;
                case "sender.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.SenderId)).Type;
                    break;
                case "sender.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.SenderId)).Alias;
                    break;
                case "target":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.TargetId);
                    break;
                case "target.id":
                    foreach (var row in rows) result[j++][i] = row.TargetId;
                    break;
                case "target.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TargetId)).Hash;
                    break;
                case "target.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TargetId)).Type;
                    break;
                case "target.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.TargetId)).Alias;
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
                case "amount":
                    foreach (var row in rows) result[j++][i] = row.Amount;
                    break;
                case "entrypoint":
                    foreach (var row in rows) result[j++][i] = row.Entrypoint;
                    break;
                case "content":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.JsonContent;
                    break;
                case "contentRaw":
                    foreach (var row in rows) result[j++][i] = Decode.ToMicheline((byte[]?)row.RawContent);
                    break;
                case "typeRaw":
                    foreach (var row in rows) result[j++][i] = Decode.ToMicheline((byte[]?)row.RawType);
                    break;
                case "counter":
                    foreach (var row in rows) result[j++][i] = row.Counter;
                    break;
                case "gasLimit":
                    foreach (var row in rows) result[j++][i] = row.GasLimit;
                    break;
                case "gasUsed":
                    foreach (var row in rows) result[j++][i] = row.GasUsed;
                    break;
                case "storageLimit":
                    foreach (var row in rows) result[j++][i] = row.StorageLimit;
                    break;
                case "storageUsed":
                    foreach (var row in rows) result[j++][i] = row.StorageUsed;
                    break;
                case "storageFee":
                    foreach (var row in rows) result[j++][i] = row.StorageFee;
                    break;
                case "status":
                    foreach (var row in rows) result[j++][i] = OperationStatuses.ToString((int)row.Status);
                    break;
                case "errors":
                    foreach (var row in rows) result[j++][i] = row.Errors;
                    break;
                case "ticketTransfers":
                    foreach (var row in rows) result[j++][i] = row.TicketTransfers;
                    break;
                case "internalOperations":
                    foreach (var row in rows) result[j++][i] = row.InternalOperations;
                    break;
                case "bakerFee":
                    foreach (var row in rows) result[j++][i] = row.BakerFee;
                    break;
                case "daFee":
                    foreach (var row in rows) result[j++][i] = row.DaFee;
                    break;
                case "gasFee":
                    foreach (var row in rows) result[j++][i] = row.GasFee;
                    break;
                case "gasRefund":
                    foreach (var row in rows) result[j++][i] = row.GasRefund;
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

    public async Task<IEnumerable<IActivity>> Activity(
        List<Data.Models.Address> addresses,
        ActivityRole roles,
        ChainInfoParameter? chain,
        DateTimeParameter? timestamp,
        CursorPagination pagination)
    {
        List<int>? senderIds = null;
        List<int>? targetIds = null;
        List<int>? ticketerIds = null;

        foreach (var address in addresses)
        {
            var count = address switch
            {
                Data.Models.L1Address a => a.TransferTicketCount,
                Data.Models.XMichelsonAddress a => a.TransferTicketCount,
                _ => 0,
            };
            if (count == 0)
                continue;

            if ((roles & ActivityRole.Sender) != 0 && address is
                Data.Models.L1User or
                Data.Models.XMichelsonUser)
            {
                senderIds ??= new(addresses.Count);
                senderIds.Add(address.Id);
            }

            if ((roles & ActivityRole.Target) != 0)
            {
                targetIds ??= new(addresses.Count);
                targetIds.Add(address.Id);
            }

            if ((roles & ActivityRole.Mention) != 0)
            {
                ticketerIds ??= new(addresses.Count);
                ticketerIds.Add(address.Id);
            }
        }

        if (senderIds == null && targetIds == null && ticketerIds == null)
            return [];

        var or = new OrParameter(
            (@"""SenderId""", senderIds),
            (@"""TargetId""", targetIds),
            (@"""TicketerId""", ticketerIds));

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

    public async Task<IEnumerable<IOpgActivity>> Activity(OperationHashEqParameter hash, ChainInfoParameter? chain, CursorPagination pagination)
    {
        return await Get(
            new() { Hash = hash.ToOperationHashParameter(), Chain = chain },
            new() { Sort = pagination.Sort, Cursor = pagination.Cursor, Limit = pagination.Limit });
    }
}
