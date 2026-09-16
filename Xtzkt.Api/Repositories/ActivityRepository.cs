using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class ActivityRepository(
    TransactionRepository _transactionRepo,
    RevealRepository _revealRepo,
    IncreasePaidStorageRepository _increasePaidStorageRepo,
    TransferTicketRepository _transferTicketRepo,
    RegisterConstantRepository _registerConstantRepo,
    DepositRepository _depositRepo,
    OriginationRepository _originationRepo,
    MigrationRepository _migrationRepo,
    TokenTransferRepository _tokenTransferRepo,
    TicketTransferRepository _ticketTransferRepo,
    BridgeTicketTransferRepository _bridgeTicketTransferRepo,
    BlockRepository _blockRepo,
    ChainCache _chainCache,
    AddressCache _addressCache,
    BlockCache _blockCache)
{
    public static readonly SortSpec SortSpec = new("id")
    {
        { "id",        (@"""Id""",        "bigint") },
        { "timestamp", (@"""Timestamp""", "timestamptz") },
    };

    public async Task<IEnumerable<IActivity>> Get(AccountActivityFilter filter, ActivityPagination pagination)
    {
        ValidatePagination(pagination);

        var chains = _chainCache.Resolve(filter.Chain);
        if (chains.Count == 0)
            return [];

        var addresses = await _addressCache.ResolveAddresses(filter.Address, chains);
        if (addresses.Count == 0)
            return [];

        var types = filter.Types?.Types ?? ActivityTypes.Default;
        if (types.Count == 0)
            return [];

        var roles = filter.Roles?.Roles ?? ActivityRoles.Default;
        if (roles == ActivityRole.None)
            return [];

        var actualChains = addresses.Select(x => x.ChainId).Distinct().Order().ToList();
        filter.Chain = actualChains.Count == _chainCache.Count() ? null : new ChainInfoParameter
        {
            Id = actualChains.Count == 1
                ? new() { Eq = actualChains[0] }
                : new() { In = actualChains },
        };

        var tasks = new List<Task<IEnumerable<IActivity>>>();

        if (types.Contains(ActivityTypes.Transaction))
            tasks.Add(_transactionRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.Reveal))
            tasks.Add(_revealRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.IncreasePaidStorage))
            tasks.Add(_increasePaidStorageRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.TransferTicket))
            tasks.Add(_transferTicketRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.RegisterConstant))
            tasks.Add(_registerConstantRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.Deposit))
            tasks.Add(_depositRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.Origination))
            tasks.Add(_originationRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.Migration))
            tasks.Add(_migrationRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.TokenTransfer))
            tasks.Add(_tokenTransferRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.TicketTransfer))
            tasks.Add(_ticketTransferRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        if (types.Contains(ActivityTypes.BridgeTicketTransfer))
            tasks.Add(_bridgeTicketTransferRepo.Activity(addresses, roles, filter.Chain, filter.Timestamp, pagination));

        await Task.WhenAll(tasks);

        return Paginate(tasks.SelectMany(x => x.Result), pagination);
    }

    public async Task<IEnumerable<IActivity>> Get(BlockActivityFilter filter, ActivityPagination pagination)
    {
        ValidatePagination(pagination);

        var types = filter.Types?.Types ?? ActivityTypes.Default;
        if (types.Count == 0)
            return [];

        if (!_chainCache.TryResolveChain(filter.Chain, out var chain))
            return [];

        if (!_blockCache.TryResolveLevel(filter.Level, chain, out var level))
            return [];

        if (await _blockRepo.GetMasks(level, chain) is not (var operations, var events))
            return [];

        var tasks = new List<Task<IEnumerable<IActivity>>>();

        if (operations.HasFlag(Data.Models.AllOperations.Transaction) && types.Contains(ActivityTypes.Transaction))
            tasks.Add(_transactionRepo.Activity(level, chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Reveal) && types.Contains(ActivityTypes.Reveal))
            tasks.Add(_revealRepo.Activity(level, chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.IncreasePaidStorage) && types.Contains(ActivityTypes.IncreasePaidStorage))
            tasks.Add(_increasePaidStorageRepo.Activity(level, chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.TransferTicket) && types.Contains(ActivityTypes.TransferTicket))
            tasks.Add(_transferTicketRepo.Activity(level, chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.RegisterConstant) && types.Contains(ActivityTypes.RegisterConstant))
            tasks.Add(_registerConstantRepo.Activity(level, chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Deposit) && types.Contains(ActivityTypes.Deposit))
            tasks.Add(_depositRepo.Activity(level, chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Origination) && types.Contains(ActivityTypes.Origination))
            tasks.Add(_originationRepo.Activity(level, chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Migration) && types.Contains(ActivityTypes.Migration))
            tasks.Add(_migrationRepo.Activity(level, chain, pagination));

        if (events.HasFlag(Data.Models.AllBlockEvents.Tokens) && types.Contains(ActivityTypes.TokenTransfer))
            tasks.Add(_tokenTransferRepo.Activity(level, chain, pagination));

        if (events.HasFlag(Data.Models.AllBlockEvents.Tickets) && types.Contains(ActivityTypes.TicketTransfer))
            tasks.Add(_ticketTransferRepo.Activity(level, chain, pagination));

        if (events.HasFlag(Data.Models.AllBlockEvents.BridgeTickets) && types.Contains(ActivityTypes.BridgeTicketTransfer))
            tasks.Add(_bridgeTicketTransferRepo.Activity(level, chain, pagination));

        if (tasks.Count == 0)
            return [];

        await Task.WhenAll(tasks);

        return Paginate(tasks.SelectMany(x => x.Result), pagination);
    }

    public async Task<IEnumerable<IOpgActivity>> Get(OpgActivityFilter filter, ActivityPagination pagination)
    {
        ValidatePagination(pagination);

        var types = filter.Types?.Types ?? ActivityTypes.Default;
        if (types.Count == 0)
            return [];
        
        var chains = _chainCache.Resolve(filter.Chain);
        if (chains.Count == 0)
            return [];

        var tasks = new List<Task<IEnumerable<IOpgActivity>>>(8)
        {
            Task.FromResult<IEnumerable<IOpgActivity>>([]),
            Task.FromResult<IEnumerable<IOpgActivity>>([]),
            Task.FromResult<IEnumerable<IOpgActivity>>([]),
            Task.FromResult<IEnumerable<IOpgActivity>>([]),
        };
        var hasTokenTransfers = types.Contains(ActivityTypes.TokenTransfer);
        var hasTicketTransfers = types.Contains(ActivityTypes.TicketTransfer);
        var hasBridgeTicketTransfers = types.Contains(ActivityTypes.BridgeTicketTransfer);

        // amend cursor pagination for transfers phase
        var (extPagination, idCursor) = ExtendPagination(pagination);

        if (types.Contains(ActivityTypes.Transaction) || hasTokenTransfers || hasTicketTransfers || hasBridgeTicketTransfers)
            tasks[0] = _transactionRepo.Activity(filter.Hash, filter.Chain, extPagination);

        if (types.Contains(ActivityTypes.Origination) || hasTokenTransfers)
            tasks[1] = _originationRepo.Activity(filter.Hash, filter.Chain, extPagination);

        if (types.Contains(ActivityTypes.TransferTicket) || hasTicketTransfers)
            tasks[2] = _transferTicketRepo.Activity(filter.Hash, filter.Chain, extPagination);

        if (types.Contains(ActivityTypes.Deposit) || hasBridgeTicketTransfers)
            tasks[3] = _depositRepo.Activity(filter.Hash, filter.Chain, extPagination);

        if (types.Contains(ActivityTypes.Reveal))
            tasks.Add(_revealRepo.Activity(filter.Hash, filter.Chain, pagination));

        if (types.Contains(ActivityTypes.IncreasePaidStorage))
            tasks.Add(_increasePaidStorageRepo.Activity(filter.Hash, filter.Chain, pagination));

        if (types.Contains(ActivityTypes.RegisterConstant))
            tasks.Add(_registerConstantRepo.Activity(filter.Hash, filter.Chain, pagination));

        await Task.WhenAll(tasks);

        if (hasTokenTransfers || hasTicketTransfers || hasBridgeTicketTransfers)
        {
            if (hasTokenTransfers)
            {
                var transactionIds = tasks[0].Result.Cast<ITokenTransfersSource>().Where(x => x.TokenTransfers > 0).Select(x => x.Id).ToList();
                var originationIds = tasks[1].Result.Cast<ITokenTransfersSource>().Where(x => x.TokenTransfers > 0).Select(x => x.Id).ToList();

                if (transactionIds.Count != 0 || originationIds.Count != 0)
                    tasks.Add(_tokenTransferRepo.Activity(transactionIds, originationIds, pagination));
            }

            if (hasTicketTransfers)
            {
                // OfType<T> is used deliberately, to filter out non-michelson txs
                var transactionIds = tasks[0].Result.OfType<ITicketTransfersSource>().Where(x => x.TicketTransfers > 0).Select(x => x.Id).ToList();
                var transferTicketIds = tasks[2].Result.Cast<ITicketTransfersSource>().Where(x => x.TicketTransfers > 0).Select(x => x.Id).ToList();

                if (transactionIds.Count != 0 || transferTicketIds.Count != 0)
                    tasks.Add(_ticketTransferRepo.Activity(transactionIds, transferTicketIds, pagination));
            }

            if (hasBridgeTicketTransfers)
            {
                // OfType<T> is used deliberately, to filter out non-evm txs and michelson deposits
                var transactionIds = tasks[0].Result.OfType<IBridgeTicketTransfersSource>().Where(x => x.BridgeTicketTransfers > 0).Select(x => x.Id).ToList();
                var depositIds = tasks[3].Result.OfType<IBridgeTicketTransfersSource>().Where(x => x.BridgeTicketTransfers > 0).Select(x => x.Id).ToList();

                if (transactionIds.Count != 0 || depositIds.Count != 0)
                    tasks.Add(_bridgeTicketTransferRepo.Activity(transactionIds, depositIds, pagination));
            }

            if (!types.Contains(ActivityTypes.Transaction)) tasks[0] = Task.FromResult<IEnumerable<IOpgActivity>>([]);
            if (!types.Contains(ActivityTypes.Origination)) tasks[1] = Task.FromResult<IEnumerable<IOpgActivity>>([]);
            if (!types.Contains(ActivityTypes.TransferTicket)) tasks[2] = Task.FromResult<IEnumerable<IOpgActivity>>([]);
            if (!types.Contains(ActivityTypes.Deposit)) tasks[3] = Task.FromResult<IEnumerable<IOpgActivity>>([]);

            await Task.WhenAll(tasks);
        }

        var items = tasks.SelectMany(x => x.Result);
        if (idCursor is long id)
            items = items.Where(x => x.Id > id);

        return Paginate(items, pagination);
    }

    static void ValidatePagination(ActivityPagination pagination)
    {
        pagination.Reduce(SortSpec);

        if (pagination.Sort!.Cols is [("timestamp", var asc1), ("id", var asc2)] && asc1 != asc2)
            throw new BadRequestException(nameof(pagination.Sort), "Sorting by different directions is not allowed for this endpoint");
    }

    static IEnumerable<T> Paginate<T>(IEnumerable<T> items, ActivityPagination pagination) where T : IActivity
    {
        var sort = pagination.Sort!;

        var result = sort.Cols[0] switch
        {
            ("id", true) => items.OrderBy(x => x.Id),
            ("id", false) => items.OrderByDescending(x => x.Id),
            ("timestamp", true) => items.OrderBy(x => x.Timestamp),
            ("timestamp", false) => items.OrderByDescending(x => x.Timestamp),
            _ => throw new Exception("Invalid sort parameter"),
        };

        foreach (var col in sort.Cols.Skip(1))
        {
            result = col switch
            {
                ("id", true) => result.ThenBy(x => x.Id),
                ("id", false) => result.ThenByDescending(x => x.Id),
                ("timestamp", true) => result.ThenBy(x => x.Timestamp),
                ("timestamp", false) => result.ThenByDescending(x => x.Timestamp),
                _ => throw new Exception("Invalid sort parameter"),
            };
        }

        return result.Take(pagination.Limit);
    }
    
    static (ActivityPagination, long?) ExtendPagination(ActivityPagination pagination)
    {
        if (pagination.Cursor?.Cols.Count > 0)
        {
            for (int i = 0; i < pagination.Cursor.Cols.Count; i++)
            {
                var (field, asc) = pagination.Sort!.Cols[i];
                if (field == "id")
                {
                    // desc sort doesn't need extension
                    if (!asc) break;

                    if (long.TryParse(pagination.Cursor.Cols[i], out var id) && id >= 0)
                    {
                        var newCursor = new CursorParameter { Cols = [.. pagination.Cursor.Cols] };
                        newCursor.Cols[i] = ((id & ~0xFFFFFL) - 1).ToString();

                        var newPagination = new ActivityPagination
                        {
                            Sort = pagination.Sort,
                            Cursor = newCursor,
                            Limit = pagination.Limit + 1,
                        };

                        return (newPagination, id);
                    }

                    // else let SqlBuilder invalidate cursor and throw bad request exception
                    break;
                }
            }
        }
        return (pagination, null);
    }
}
