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

public class TransactionRepository(
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

    async Task<bool> ProcessFilters(TransactionOperationFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Sender?.Hash != null)
            filter.Sender.Id += await filter.Sender.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Target?.Hash != null)
            filter.Target.Id += await filter.Target.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Initiator?.Hash != null)
            filter.Initiator.Id += await filter.Initiator.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Alias?.Hash != null)
            filter.Alias.Id += await filter.Alias.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Gateway?.Hash != null)
            filter.Gateway.Id += await filter.Gateway.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(TransactionOperationFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "direction":              columns.Add(@"""Direction"""); break;
                    case "id":                     columns.Add(@"""Id"""); break;
                    case "chain":                  columns.Add(@"""ChainId"""); break;
                    case "level":                  columns.Add(@"""Level"""); break;
                    case "timestamp":              columns.Add(@"""Timestamp"""); break;
                    case "hash":                   columns.Add(@"""Hash"""); break;
                    case "sender":                 columns.Add(@"""SenderId"""); break;
                    case "senderCodeHash":         columns.Add(@"""SenderCodeHash"""); break;
                    case "initiator":              columns.Add(@"""InitiatorId"""); break;
                    case "target":                 columns.Add(@"""TargetId"""); break;
                    case "targetCodeHash":         columns.Add(@"""TargetCodeHash"""); break;
                    case "counter":                columns.Add(@"""Counter"""); break;
                    case "gasLimit":               columns.Add(@"""GasLimit"""); break;
                    case "gasUsed":                columns.Add(@"""GasUsed"""); break;
                    case "status":                 columns.Add(@"""Status"""); break;
                    case "errors":                 columns.Add(@"""Errors"""); break;
                    case "entrypoint":             columns.Add(@"""Entrypoint"""); break;
                    case "parameters":
                        if (field.Path == null)
                        {
                            columns.Add(@"""Parameters""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"""Parameters"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    case "guessed":                columns.Add(@"""Guessed"""); break;
                    case "tokenTransfers":         columns.Add(@"""TokenTransfers"""); break;
                    case "internalOperations":     columns.Add(@"""InternalOperations"""); break;
                    case "logsCount":              columns.Add(@"""LogsCount"""); break;
                    case "amount":                 columns.Add(@"""Amount"""); columns.Add(@"""Amount18"""); columns.Add(@"""Direction"""); break;
                    case "amountSent":             columns.Add(@"""Amount"""); columns.Add(@"""Amount18"""); columns.Add(@"""Direction"""); break;
                    case "amountReceived":         columns.Add(@"""Amount"""); columns.Add(@"""Amount18"""); columns.Add(@"""Direction"""); break;
                    case "daFee":                  columns.Add(@"""DaFee"""); columns.Add(@"""DaFee18"""); columns.Add(@"""Direction"""); break;
                    case "gasFee":                 columns.Add(@"""GasFee"""); columns.Add(@"""GasFee18"""); columns.Add(@"""Direction"""); break;
                    case "gasRefund":              columns.Add(@"""GasRefund"""); break;
                    case "storageFee":             columns.Add(@"""StorageFee"""); break;
                    case "allocationFee":          columns.Add(@"""AllocationFee"""); break;
                    case "storageLimit":           columns.Add(@"""StorageLimit"""); break;
                    case "storageUsed":            columns.Add(@"""StorageUsed"""); break;
                    case "nonce":                  columns.Add(@"""Nonce"""); break;
                    case "bigMapUpdates":          columns.Add(@"""BigMapUpdates"""); break;
                    case "ticketTransfers":        columns.Add(@"""TicketTransfers"""); break;
                    case "parametersRaw":          columns.Add(@"""ParametersRaw"""); break;
                    case "bakerFee":               columns.Add(@"""BakerFee"""); break;
                    case "opType":                 columns.Add(@"""OpType"""); break;
                    case "opCode":                 columns.Add(@"""OpCode"""); break;
                    case "gasPrice":               columns.Add(@"""GasPrice"""); break;
                    case "maxFeePerGas":           columns.Add(@"""MaxFeePerGas"""); break;
                    case "maxPriorityFeePerGas":   columns.Add(@"""MaxPriorityFeePerGas"""); break;
                    case "effectiveGasPrice":      columns.Add(@"""EffectiveGasPrice"""); break;
                    case "input":                  columns.Add(@"""Input"""); break;
                    case "output":                 columns.Add(@"""Output"""); break;
                    case "result":
                        if (field.Path == null)
                        {
                            columns.Add(@"""Result""");
                        }
                        else
                        {
                            field.Column = $"c{counter++}";
                            columns.Add($@"""Result"" #> '{{{field.PathString}}}' as {field.Column}");
                        }
                        break;
                    case "eip7702DelegationCount": columns.Add(@"""Eip7702DelegationCount"""); break;
                    case "bridgeTicketTransfers":  columns.Add(@"""BridgeTicketTransfers"""); break;
                    case "claimDepositId":         columns.Add(@"""ClaimDepositId"""); break;
                    case "roundingLoss":           columns.Add(@"""RoundingLoss"""); break;
                    case "alias":                  columns.Add(@"""AliasId"""); break;
                    case "gateway":                columns.Add(@"""GatewayId"""); break;
                    case "gatewayEntrypoint":      columns.Add(@"""GatewayEntrypoint"""); break;
                    case "gatewayParameters":      columns.Add(@"""GatewayParameters"""); break;
                    case "gatewayInput":           columns.Add(@"""GatewayInput"""); break;
                    case "gatewayParametersRaw":   columns.Add(@"""GatewayParametersRaw"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""TransactionOps""")
            .Where(filter.Or)
            .Where(filter.Anyof, x => x switch
            {
                "sender" => @"""SenderId""",
                "target" => @"""TargetId""",
                "initiator" => @"""InitiatorId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `sender`, `target` and `initiator` fields only."),
            })
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""Level""",          filter.Level)
            .Where(@"""Timestamp""",      filter.Timestamp)
            .Where(@"""Hash""",           filter.Hash)
            .Where(@"""SenderId""",       filter.Sender?.Id)
            .Where(@"""SenderCodeHash""", filter.SenderCodeHash)
            .Where(@"""Counter""",        filter.Counter)
            .Where(@"""Status""",         filter.Status)
            .Where(@"""TargetId""",       filter.Target?.Id)
            .Where(@"""TargetCodeHash""", filter.TargetCodeHash)
            .Where(@"""InitiatorId""",    filter.Initiator?.Id)
            .Where(@"""Entrypoint""",     filter.Entrypoint)
            .Where(@"""Parameters""",     filter.Parameters)
            .Where(@"""Guessed""",        filter.Guessed)
            .Where(@"""AliasId""",        filter.Alias?.Id)
            .Where(@"""GatewayId""",      filter.Gateway?.Id)
            .Where(@"""ClaimDepositId""", filter.ClaimDepositId)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(TransactionOperationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().Sum(x => x.TransactionOpsCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""TransactionOps""")
            .Where(filter.Anyof, x => x switch
            {
                "sender" => @"""SenderId""",
                "target" => @"""TargetId""",
                "initiator" => @"""InitiatorId""",
                _ => throw new BadRequestException(nameof(filter.Anyof), "This parameter can be used with `sender`, `target` and `initiator` fields only."),
            })
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""Level""",          filter.Level)
            .Where(@"""Timestamp""",      filter.Timestamp)
            .Where(@"""Hash""",           filter.Hash)
            .Where(@"""SenderId""",       filter.Sender?.Id)
            .Where(@"""SenderCodeHash""", filter.SenderCodeHash)
            .Where(@"""Counter""",        filter.Counter)
            .Where(@"""Status""",         filter.Status)
            .Where(@"""TargetId""",       filter.Target?.Id)
            .Where(@"""TargetCodeHash""", filter.TargetCodeHash)
            .Where(@"""InitiatorId""",    filter.Initiator?.Id)
            .Where(@"""Entrypoint""",     filter.Entrypoint)
            .Where(@"""Parameters""",     filter.Parameters)
            .Where(@"""Guessed""",        filter.Guessed)
            .Where(@"""AliasId""",        filter.Alias?.Id)
            .Where(@"""GatewayId""",      filter.Gateway?.Id)
            .Where(@"""ClaimDepositId""", filter.ClaimDepositId)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<TransactionOperation>> Get(TransactionOperationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select<dynamic, TransactionOperation>(row =>
        {
            return (Data.Models.Direction)row.Direction switch
            {
                Data.Models.Direction.L1 => new L1TransactionOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Target = _addressCache.GetInfo((int)row.TargetId),
                    TargetCodeHash = row.TargetCodeHash,
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Entrypoint = row.Entrypoint,
                    Parameters = row.Parameters,
                    Guessed = row.Guessed,
                    TokenTransfers = row.TokenTransfers,
                    InternalOperations = row.InternalOperations,
                    LogsCount = row.LogsCount,
                    Amount = row.Amount,
                    StorageFee = row.StorageFee,
                    AllocationFee = row.AllocationFee,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Nonce = row.Nonce,
                    BigMapUpdates = row.BigMapUpdates,
                    TicketTransfers = row.TicketTransfers,
                    ParametersRaw = Decode.ToMicheline((byte[]?)row.ParametersRaw),
                    BakerFee = row.BakerFee,
                },
                Data.Models.Direction.XMichelson => new XMichelsonTransactionOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Target = _addressCache.GetInfo((int)row.TargetId),
                    TargetCodeHash = row.TargetCodeHash,
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Entrypoint = row.Entrypoint,
                    Parameters = row.Parameters,
                    Guessed = row.Guessed,
                    TokenTransfers = row.TokenTransfers,
                    InternalOperations = row.InternalOperations,
                    LogsCount = row.LogsCount,
                    Amount = row.Amount,
                    StorageFee = row.StorageFee,
                    AllocationFee = row.AllocationFee,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Nonce = row.Nonce,
                    BigMapUpdates = row.BigMapUpdates,
                    TicketTransfers = row.TicketTransfers,
                    ParametersRaw = Decode.ToMicheline((byte[]?)row.ParametersRaw),
                    DaFee = row.DaFee,
                    GasFee = row.GasFee,
                    GasRefund = row.GasRefund,
                },
                Data.Models.Direction.XEvm => new XEvmTransactionOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Target = _addressCache.GetInfo((int)row.TargetId),
                    TargetCodeHash = row.TargetCodeHash,
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Entrypoint = row.Entrypoint,
                    Parameters = row.Parameters,
                    Guessed = row.Guessed,
                    TokenTransfers = row.TokenTransfers,
                    InternalOperations = row.InternalOperations,
                    LogsCount = row.LogsCount,
                    OpType = EvmOpTypes.ToString((int)row.OpType),
                    OpCode = EvmOpCodes.ToString((int)row.OpCode),
                    Amount = row.Amount18,
                    DaFee = row.DaFee18,
                    GasFee = row.GasFee18,
                    GasPrice = row.GasPrice,
                    MaxFeePerGas = row.MaxFeePerGas,
                    MaxPriorityFeePerGas = row.MaxPriorityFeePerGas,
                    EffectiveGasPrice = row.EffectiveGasPrice,
                    Input = row.Input,
                    Output = row.Output,
                    Result = row.Result,
                    Eip7702DelegationCount = row.Eip7702DelegationCount,
                    BridgeTicketTransfers = row.BridgeTicketTransfers,
                    ClaimDepositId = row.ClaimDepositId,
                },
                Data.Models.Direction.XEvmMichelson => new XEvmMichelsonTransactionOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Target = _addressCache.GetInfo((int)row.TargetId),
                    TargetCodeHash = row.TargetCodeHash,
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Entrypoint = row.Entrypoint,
                    Parameters = row.Parameters,
                    Guessed = row.Guessed,
                    TokenTransfers = row.TokenTransfers,
                    InternalOperations = row.InternalOperations,
                    LogsCount = row.LogsCount,
                    OpType = EvmOpTypes.ToString((int)row.OpType),
                    OpCode = EvmOpCodes.ToString((int)row.OpCode),
                    AmountSent = row.Amount18,
                    RoundingLoss = row.RoundingLoss,
                    AmountReceived = row.Amount,
                    DaFee = row.DaFee18,
                    GasFee = row.GasFee18,
                    GasPrice = row.GasPrice,
                    MaxFeePerGas = row.MaxFeePerGas,
                    MaxPriorityFeePerGas = row.MaxPriorityFeePerGas,
                    EffectiveGasPrice = row.EffectiveGasPrice,
                    BigMapUpdates = row.BigMapUpdates,
                    TicketTransfers = row.TicketTransfers,
                    ParametersRaw = Decode.ToMicheline((byte[]?)row.ParametersRaw),
                    Alias = _addressCache.GetInfo((int)row.AliasId),
                    Gateway = _addressCache.GetInfo((int)row.GatewayId),
                    GatewayEntrypoint = row.GatewayEntrypoint,
                    GatewayParameters = row.GatewayParameters,
                    GatewayInput = row.GatewayInput,
                    Eip7702DelegationCount = row.Eip7702DelegationCount,
                },
                Data.Models.Direction.XMichelsonEvm => new XMichelsonEvmTransactionOperation
                {
                    Id = row.Id,
                    Chain = _chainCache.GetInfo((int)row.ChainId),
                    Level = row.Level,
                    Timestamp = row.Timestamp,
                    Hash = row.Hash,
                    Sender = _addressCache.GetInfo((int)row.SenderId),
                    SenderCodeHash = row.SenderCodeHash,
                    Initiator = _addressCache.GetInfo((int?)row.InitiatorId),
                    Target = _addressCache.GetInfo((int)row.TargetId),
                    TargetCodeHash = row.TargetCodeHash,
                    Counter = row.Counter,
                    GasLimit = row.GasLimit,
                    GasUsed = row.GasUsed,
                    Status = OperationStatuses.ToString((int)row.Status),
                    Errors = row.Errors,
                    Entrypoint = row.Entrypoint,
                    Parameters = row.Parameters,
                    Guessed = row.Guessed,
                    TokenTransfers = row.TokenTransfers,
                    InternalOperations = row.InternalOperations,
                    LogsCount = row.LogsCount,
                    AmountSent = row.Amount,
                    AmountReceived = row.Amount18,
                    DaFee = row.DaFee,
                    GasFee = row.GasFee,
                    GasRefund = row.GasRefund,
                    StorageFee = row.StorageFee,
                    AllocationFee = row.AllocationFee,
                    StorageLimit = row.StorageLimit,
                    StorageUsed = row.StorageUsed,
                    Nonce = row.Nonce,
                    Input = row.Input,
                    Output = row.Output,
                    Result = row.Result,
                    Alias = _addressCache.GetInfo((int)row.AliasId),
                    Gateway = _addressCache.GetInfo((int)row.GatewayId),
                    GatewayEntrypoint = row.GatewayEntrypoint,
                    GatewayParameters = row.GatewayParameters,
                    GatewayParametersRaw = Decode.ToMicheline((byte[]?)row.GatewayParametersRaw),
                    BridgeTicketTransfers = row.BridgeTicketTransfers,
                    ClaimDepositId = row.ClaimDepositId,
                },
                _ => throw new InvalidOperationException("Failed to read TransactionOperation")
            };
        });
    }

    public async Task<object?[][]> Get(TransactionOperationFilter filter, Pagination pagination, Selection selection)
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
                case "direction":
                    foreach (var row in rows) result[j++][i] = Directions.ToString((int)row.Direction);
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
                case "targetCodeHash":
                    foreach (var row in rows) result[j++][i] = row.TargetCodeHash;
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
                case "entrypoint":
                    foreach (var row in rows) result[j++][i] = row.Entrypoint;
                    break;
                case "parameters":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Parameters;
                    break;
                case "guessed":
                    foreach (var row in rows) result[j++][i] = row.Guessed;
                    break;
                case "tokenTransfers":
                    foreach (var row in rows) result[j++][i] = row.TokenTransfers;
                    break;
                case "internalOperations":
                    foreach (var row in rows) result[j++][i] = row.InternalOperations;
                    break;
                case "logsCount":
                    foreach (var row in rows) result[j++][i] = row.LogsCount;
                    break;
                case "amount":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Direction)(int)row.Direction switch
                    {
                        Data.Models.Direction.L1 or Data.Models.Direction.XMichelson => row.Amount,
                        Data.Models.Direction.XEvm => row.Amount18,
                        _ => null
                    };
                    break;
                case "amountSent":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Direction)(int)row.Direction switch
                    {
                        Data.Models.Direction.XEvmMichelson => row.Amount18,
                        Data.Models.Direction.XMichelsonEvm => row.Amount,
                        _ => null
                    };
                    break;
                case "amountReceived":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Direction)(int)row.Direction switch
                    {
                        Data.Models.Direction.XEvmMichelson => row.Amount,
                        Data.Models.Direction.XMichelsonEvm => row.Amount18,
                        _ => null
                    };
                    break;
                case "daFee":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Direction)(int)row.Direction switch
                    {
                        Data.Models.Direction.XEvm or Data.Models.Direction.XEvmMichelson => row.DaFee18,
                        Data.Models.Direction.XMichelson or Data.Models.Direction.XMichelsonEvm => row.DaFee,
                        _ => null
                    };
                    break;
                case "gasFee":
                    foreach (var row in rows) result[j++][i] = (Data.Models.Direction)(int)row.Direction switch
                    {
                        Data.Models.Direction.XEvm or Data.Models.Direction.XEvmMichelson => row.GasFee18,
                        Data.Models.Direction.XMichelson or Data.Models.Direction.XMichelsonEvm => row.GasFee,
                        _ => null
                    };
                    break;
                case "gasRefund":
                    foreach (var row in rows) result[j++][i] = row.GasRefund;
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
                case "ticketTransfers":
                    foreach (var row in rows) result[j++][i] = row.TicketTransfers;
                    break;
                case "parametersRaw":
                    foreach (var row in rows) result[j++][i] = Decode.ToMicheline((byte[]?)row.ParametersRaw);
                    break;
                case "bakerFee":
                    foreach (var row in rows) result[j++][i] = row.BakerFee;
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
                case "input":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[]?)row.Input);
                    break;
                case "output":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[]?)row.Output);
                    break;
                case "result":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.Result;
                    break;
                case "eip7702DelegationCount":
                    foreach (var row in rows) result[j++][i] = row.Eip7702DelegationCount;
                    break;
                case "bridgeTicketTransfers":
                    foreach (var row in rows) result[j++][i] = row.BridgeTicketTransfers;
                    break;
                case "claimDepositId":
                    foreach (var row in rows) result[j++][i] = row.ClaimDepositId?.ToString();
                    break;
                case "roundingLoss":
                    foreach (var row in rows) result[j++][i] = row.RoundingLoss;
                    break;
                case "alias":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.AliasId);
                    break;
                case "alias.id":
                    foreach (var row in rows) result[j++][i] = row.AliasId;
                    break;
                case "alias.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.AliasId))?.Hash;
                    break;
                case "alias.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.AliasId))?.Type;
                    break;
                case "alias.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.AliasId))?.Alias;
                    break;
                case "gateway":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.GatewayId);
                    break;
                case "gateway.id":
                    foreach (var row in rows) result[j++][i] = row.GatewayId;
                    break;
                case "gateway.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.GatewayId))?.Hash;
                    break;
                case "gateway.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.GatewayId))?.Type;
                    break;
                case "gateway.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.GatewayId))?.Alias;
                    break;
                case "gatewayEntrypoint":
                    foreach (var row in rows) result[j++][i] = row.GatewayEntrypoint;
                    break;
                case "gatewayParameters":
                    foreach (var row in rows) result[j++][i] = (RawJson?)row.GatewayParameters;
                    break;
                case "gatewayInput":
                    foreach (var row in rows) result[j++][i] = Decode.ToHex((byte[]?)row.GatewayInput);
                    break;
                case "gatewayParametersRaw":
                    foreach (var row in rows) result[j++][i] = Decode.ToMicheline((byte[]?)row.GatewayParametersRaw);
                    break;
                default:
                    if (fields[i].Field is "parameters" or "result")
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
        List<int>? initiatorIds = null;
        List<int>? aliasIds = null;
        List<int>? gatewayIds = null;

        foreach (var address in addresses)
        {
            if (address.TransactionsCount == 0)
                continue;

            if ((roles & ActivityRole.Sender) != 0)
            {
                senderIds ??= new(addresses.Count);
                senderIds.Add(address.Id);

                if (address is Data.Models.XEvmAlias ||
                    address is Data.Models.XMichelsonAlias)
                {
                    aliasIds ??= new(addresses.Count);
                    aliasIds.Add(address.Id);
                }
            }

            if ((roles & ActivityRole.Target) != 0)
            {
                targetIds ??= new(addresses.Count);
                targetIds.Add(address.Id);

                if (address.OriginationsCount == 0 &&
                    (address is Data.Models.XEvmContract || address is Data.Models.XMichelsonContract))
                {
                    gatewayIds ??= new(addresses.Count);
                    gatewayIds.Add(address.Id);
                }
            }

            if ((roles & ActivityRole.Initiator) != 0)
            {
                if (address is Data.Models.L1User ||
                    address is Data.Models.XEvmUser ||
                    address is Data.Models.XMichelsonUser)
                {
                    initiatorIds ??= new(addresses.Count);
                    initiatorIds.Add(address.Id);
                }
            }
        }

        if (senderIds == null && targetIds == null && initiatorIds == null && aliasIds == null && gatewayIds == null)
            return [];

        var or = new OrParameter(
            (@"""SenderId""", senderIds),
            (@"""TargetId""", targetIds),
            (@"""InitiatorId""", initiatorIds),
            (@"""AliasId""", aliasIds),
            (@"""GatewayId""", gatewayIds));

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
