using Dapper;
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

public class OriginationRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    NpgsqlDataSource _dataSource)
{
    static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"""Id""",        "bigint") },
        { "level",     (@"""Level""",     "integer") },
        { "timestamp", (@"""Timestamp""", "timestamptz") },
    };

    async Task<bool> ProcessFilters(OriginationOperationFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Sender?.Hash != null)
            filter.Sender.Id += await filter.Sender.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Initiator?.Hash != null)
            filter.Initiator.Id += await filter.Initiator.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Contract?.Hash != null)
            filter.Contract.Id += await filter.Contract.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Baker?.Hash != null)
            filter.Baker.Id += await filter.Baker.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(OriginationOperationFilter filter, Pagination pagination, Selection? selection = null)
    {
        if (!await ProcessFilters(filter))
            return [];

        var columns = new HashSet<string>();
        if (selection != null)
        {
            foreach (var field in selection.Fields())
            {
                switch (field.Field)
                {
                    case "env":                    columns.Add(@"""Env"""); break;
                    case "id":                     columns.Add(@"""Id"""); break;
                    case "chain":                  columns.Add(@"""ChainId"""); break;
                    case "level":                  columns.Add(@"""Level"""); break;
                    case "timestamp":              columns.Add(@"""Timestamp"""); break;
                    case "hash":                   columns.Add(@"""Hash"""); break;
                    case "sender":                 columns.Add(@"""SenderId"""); break;
                    case "senderCodeHash":         columns.Add(@"""SenderCodeHash"""); break;
                    case "initiator":              columns.Add(@"""InitiatorId"""); break;
                    case "counter":                columns.Add(@"""Counter"""); break;
                    case "gasLimit":               columns.Add(@"""GasLimit"""); break;
                    case "gasUsed":                columns.Add(@"""GasUsed"""); break;
                    case "status":                 columns.Add(@"""Status"""); break;
                    case "errors":                 columns.Add(@"""Errors"""); break;
                    case "contract":               columns.Add(@"""ContractId"""); break;
                    case "contractCodeHash":       columns.Add(@"""ContractCodeHash"""); break;
                    case "tokenTransfers":         columns.Add(@"""TokenTransfers"""); break;
                    case "storageFee":             columns.Add(@"""StorageFee"""); break;
                    case "allocationFee":          columns.Add(@"""AllocationFee"""); break;
                    case "storageLimit":           columns.Add(@"""StorageLimit"""); break;
                    case "storageUsed":            columns.Add(@"""StorageUsed"""); break;
                    case "nonce":                  columns.Add(@"""Nonce"""); break;
                    case "bigMapUpdates":          columns.Add(@"""BigMapUpdates"""); break;
                    case "balance":                columns.Add(@"""Balance"""); columns.Add(@"""Balance18"""); columns.Add(@"""Env"""); break;
                    case "bakerFee":               columns.Add(@"""BakerFee"""); break;
                    case "baker":                  columns.Add(@"""BakerId"""); break;
                    case "daFee":                  columns.Add(@"""DaFee"""); columns.Add(@"""DaFee18"""); columns.Add(@"""Env"""); break;
                    case "gasFee":                 columns.Add(@"""GasFee"""); columns.Add(@"""GasFee18"""); columns.Add(@"""Env"""); break;
                    case "gasRefund":              columns.Add(@"""GasRefund"""); break;
                    case "opType":                 columns.Add(@"""OpType"""); break;
                    case "opCode":                 columns.Add(@"""OpCode"""); break;
                    case "gasPrice":               columns.Add(@"""GasPrice"""); break;
                    case "maxFeePerGas":           columns.Add(@"""MaxFeePerGas"""); break;
                    case "maxPriorityFeePerGas":   columns.Add(@"""MaxPriorityFeePerGas"""); break;
                    case "effectiveGasPrice":      columns.Add(@"""EffectiveGasPrice"""); break;
                    case "internalOperations":     columns.Add(@"""InternalOperations"""); break;
                    case "logsCount":              columns.Add(@"""LogsCount"""); break;
                    case "reOriginated":           columns.Add(@"""ReOriginated"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""OriginationOps""")
            .Where(filter.Or)
            .Where(filter.Anyof, x => x switch
            {
                "sender" => @"""SenderId""",
                "contract" => @"""ContractId""",
                "baker" => @"""BakerId""",
                "initiator" => @"""InitiatorId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `sender`, `contract`, `baker` and `initiator` fields only."),
            })
            .Where(@"""Id""",               filter.Id)
            .Where(@"""ChainId""",          filter.Chain?.Id)
            .Where(@"""Level""",            filter.Level)
            .Where(@"""Timestamp""",        filter.Timestamp)
            .Where(@"""Hash""",             filter.Hash)
            .Where(@"""SenderId""",         filter.Sender?.Id)
            .Where(@"""SenderCodeHash""",   filter.SenderCodeHash)
            .Where(@"""Counter""",          filter.Counter)
            .Where(@"""Status""",           filter.Status)
            .Where(@"""InitiatorId""",      filter.Initiator?.Id)
            .Where(@"""ContractId""",       filter.Contract?.Id)
            .Where(@"""ContractCodeHash""", filter.ContractCodeHash)
            .Where(@"""BakerId""",          filter.Baker?.Id)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(OriginationOperationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.OriginationOpsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""OriginationOps""")
            .Where(filter.Anyof, x => x switch
            {
                "sender" => @"""SenderId""",
                "contract" => @"""ContractId""",
                "baker" => @"""BakerId""",
                "initiator" => @"""InitiatorId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `sender`, `contract`, `baker` and `initiator` fields only."),
            })
            .Where(@"""Id""",               filter.Id)
            .Where(@"""ChainId""",          filter.Chain?.Id)
            .Where(@"""Level""",            filter.Level)
            .Where(@"""Timestamp""",        filter.Timestamp)
            .Where(@"""Hash""",             filter.Hash)
            .Where(@"""SenderId""",         filter.Sender?.Id)
            .Where(@"""SenderCodeHash""",   filter.SenderCodeHash)
            .Where(@"""Counter""",          filter.Counter)
            .Where(@"""Status""",           filter.Status)
            .Where(@"""InitiatorId""",      filter.Initiator?.Id)
            .Where(@"""ContractId""",       filter.Contract?.Id)
            .Where(@"""ContractCodeHash""", filter.ContractCodeHash)
            .Where(@"""BakerId""",          filter.Baker?.Id)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<OriginationOperation>> Get(OriginationOperationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, OriginationOperation>(row =>
        {
            return (Data.Models.Env)(int)row.Env switch
            {
                Data.Models.Env.L1 => new L1OriginationOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Contract = _addressCache.GetInfo((int?)row.ContractId),
                    ContractCodeHash = row.ContractCodeHash,
                    TokenTransfers = row.TokenTransfers,
                    StorageFee = row.StorageFee,
                    AllocationFee = row.AllocationFee,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Nonce = row.Nonce,
                    BigMapUpdates = row.BigMapUpdates,
                    Balance = row.Balance,
                    BakerFee = row.BakerFee,
                    Baker = _addressCache.GetInfo((int?)row.BakerId),
                },
                Data.Models.Env.XMichelson => new XMichelsonOriginationOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Contract = _addressCache.GetInfo((int?)row.ContractId),
                    ContractCodeHash = row.ContractCodeHash,
                    TokenTransfers = row.TokenTransfers,
                    StorageFee = row.StorageFee,
                    AllocationFee = row.AllocationFee,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Nonce = row.Nonce,
                    BigMapUpdates = row.BigMapUpdates,
                    Balance = row.Balance,
                    DaFee = row.DaFee,
                    GasFee = row.GasFee,
                    GasRefund = row.GasRefund,
                },
                Data.Models.Env.XEvm => new XEvmOriginationOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Contract = _addressCache.GetInfo((int?)row.ContractId),
                    ContractCodeHash = row.ContractCodeHash,
                    TokenTransfers = row.TokenTransfers,
                    OpType = EvmOpTypes.ToString((int)row.OpType),
                    OpCode = EvmOpCodes.ToString((int)row.OpCode),
                    Balance = row.Balance18,
                    DaFee = row.DaFee18,
                    GasFee = row.GasFee18,
                    GasPrice = row.GasPrice,
                    MaxFeePerGas = row.MaxFeePerGas,
                    MaxPriorityFeePerGas = row.MaxPriorityFeePerGas,
                    EffectiveGasPrice = row.EffectiveGasPrice,
                    InternalOperations = row.InternalOperations,
                    LogsCount = row.LogsCount,
                    ReOriginated = row.ReOriginated,
                },
                _ => throw new InvalidOperationException("Failed to read OriginationOperation")
            };
        });
    }

    public async Task<object?[][]> Get(OriginationOperationFilter filter, Pagination pagination, Selection selection)
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
                case "env":
                    foreach (var row in rows) result[j++][i] = Envs.ToString((int)row.Env);
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
                case "senderCodeHash":
                    foreach (var row in rows) result[j++][i] = row.SenderCodeHash;
                    break;
                case "initiator":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.InitiatorId);
                    break;
                case "initiator.id":
                    foreach (var row in rows) result[j++][i] = row.InitiatorId;
                    break;
                case "initiator.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.InitiatorId))?.Hash;
                    break;
                case "initiator.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.InitiatorId))?.Type;
                    break;
                case "initiator.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.InitiatorId))?.Alias;
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
                case "status":
                    foreach (var row in rows) result[j++][i] = OperationStatuses.ToString((int)row.Status);
                    break;
                case "errors":
                    foreach (var row in rows) result[j++][i] = row.Errors;
                    break;
                case "contract":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.ContractId);
                    break;
                case "contract.id":
                    foreach (var row in rows) result[j++][i] = row.ContractId;
                    break;
                case "contract.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ContractId))?.Hash;
                    break;
                case "contract.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ContractId))?.Type;
                    break;
                case "contract.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.ContractId))?.Alias;
                    break;
                case "contractCodeHash":
                    foreach (var row in rows) result[j++][i] = row.ContractCodeHash;
                    break;
                case "tokenTransfers":
                    foreach (var row in rows) result[j++][i] = row.TokenTransfers;
                    break;
                case "storageFee":
                    foreach (var row in rows) result[j++][i] = row.StorageFee;
                    break;
                case "allocationFee":
                    foreach (var row in rows) result[j++][i] = row.AllocationFee;
                    break;
                case "storageLimit":
                    foreach (var row in rows) result[j++][i] = row.StorageLimit;
                    break;
                case "storageUsed":
                    foreach (var row in rows) result[j++][i] = row.StorageUsed;
                    break;
                case "nonce":
                    foreach (var row in rows) result[j++][i] = row.Nonce;
                    break;
                case "bigMapUpdates":
                    foreach (var row in rows) result[j++][i] = row.BigMapUpdates;
                    break;
                case "balance":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Env)(int)row.Env switch
                    {
                        Data.Models.Env.XEvm => row.Balance18,
                        _ => row.Balance
                    };
                    break;
                case "bakerFee":
                    foreach (var row in rows) result[j++][i] = row.BakerFee;
                    break;
                case "baker":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.BakerId);
                    break;
                case "baker.id":
                    foreach (var row in rows) result[j++][i] = row.BakerId;
                    break;
                case "baker.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.BakerId))?.Hash;
                    break;
                case "baker.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.BakerId))?.Type;
                    break;
                case "baker.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.BakerId))?.Alias;
                    break;
                case "daFee":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Env)(int)row.Env switch
                    {
                        Data.Models.Env.XMichelson => row.DaFee,
                        Data.Models.Env.XEvm => row.DaFee18,
                        _ => null
                    };
                    break;
                case "gasFee":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Env)(int)row.Env switch
                    {
                        Data.Models.Env.XMichelson => row.GasFee,
                        Data.Models.Env.XEvm => row.GasFee18,
                        _ => null
                    };
                    break;
                case "gasRefund":
                    foreach (var row in rows) result[j++][i] = row.GasRefund;
                    break;
                case "opType":
                    foreach (var row in rows) result[j++][i] = row.OpType == null ? null : EvmOpTypes.ToString((int)row.OpType);
                    break;
                case "opCode":
                    foreach (var row in rows) result[j++][i] = row.OpCode == null ? null : EvmOpCodes.ToString((int)row.OpCode);
                    break;
                case "gasPrice":
                    foreach (var row in rows) result[j++][i] = row.GasPrice;
                    break;
                case "maxFeePerGas":
                    foreach (var row in rows) result[j++][i] = row.MaxFeePerGas;
                    break;
                case "maxPriorityFeePerGas":
                    foreach (var row in rows) result[j++][i] = row.MaxPriorityFeePerGas;
                    break;
                case "effectiveGasPrice":
                    foreach (var row in rows) result[j++][i] = row.EffectiveGasPrice;
                    break;
                case "internalOperations":
                    foreach (var row in rows) result[j++][i] = row.InternalOperations;
                    break;
                case "logsCount":
                    foreach (var row in rows) result[j++][i] = row.LogsCount;
                    break;
                case "reOriginated":
                    foreach (var row in rows) result[j++][i] = row.ReOriginated;
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
        List<int>? contractIds = null;
        List<int>? initiatorIds = null;
        List<int>? bakerIds = null;

        foreach (var address in addresses)
        {
            if (address.OriginationsCount == 0)
                continue;

            if ((roles & ActivityRole.Sender) != 0)
            {
                senderIds ??= new(addresses.Count);
                senderIds.Add(address.Id);
            }

            if ((roles & ActivityRole.Target) != 0 && address is
                Data.Models.L1Contract or
                Data.Models.XEvmContract or
                Data.Models.XMichelsonContract)
            {
                contractIds ??= new(addresses.Count);
                contractIds.Add(address.Id);
            }

            if ((roles & ActivityRole.Initiator) != 0 && address is
                Data.Models.L1User or
                Data.Models.XEvmUser or
                Data.Models.XMichelsonUser)
            {
                initiatorIds ??= new(addresses.Count);
                initiatorIds.Add(address.Id);
            }

            if ((roles & ActivityRole.Mention) != 0 && address is
                Data.Models.L1Baker)
            {
                bakerIds ??= new(addresses.Count);
                bakerIds.Add(address.Id);
            }
        }

        if (senderIds == null && contractIds == null && initiatorIds == null && bakerIds == null)
            return [];

        var or = new OrParameter(
            (@"""SenderId""", senderIds),
            (@"""ContractId""", contractIds),
            (@"""InitiatorId""", initiatorIds),
            (@"""BakerId""", bakerIds));

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
