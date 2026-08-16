using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Repositories.Operations;
using Xtzkt.Api.Services.Cache;

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
    BlockRepository _blockRepo,
    ChainCache _chainCache,
    AddressCache _addressCache)
{
    public async Task<IEnumerable<IActivity>> Get(AccountActivityFilter filter, CursorPagination pagination)
    {
        ValidatePagination(pagination);

        var addresses = new List<Data.Models.Address>();
        if ((filter.Chain?.Id + filter.Chain?.ChainId?.ToIdParameter(_chainCache))?.Eq is int _chainId)
        {
            if (filter.Address.Eq != null)
            {
                if (await _addressCache.GetAsync(_chainId, filter.Address.Eq) is Data.Models.Address address)
                    addresses.Add(address);
            }
            else
            {
                addresses.AddRange(await _addressCache.GetAsync(_chainId, filter.Address.In!));
            }
        }
        else
        {
            if (filter.Address.Eq != null)
            {
                addresses.AddRange(await _addressCache.GetAsync(filter.Address.Eq));
            }
            else
            {
                addresses.AddRange(await _addressCache.GetAsync(filter.Address.In!));
            }
        }

        if (addresses.Count == 0)
            return [];

        var types = filter.Types?.Types ?? ActivityTypes.Default;
        if (types.Count == 0)
            return [];

        var roles = filter.Roles?.Roles ?? ActivityRoles.Default;
        if (roles == ActivityRole.None)
            return [];

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

        await Task.WhenAll(tasks);

        return Paginate(tasks.SelectMany(x => x.Result), pagination);
    }

    public async Task<IEnumerable<IActivity>> Get(BlockActivityFilter filter, CursorPagination pagination)
    {
        ValidatePagination(pagination);

        var types = filter.Types?.Types ?? ActivityTypes.Default;
        if (types.Count == 0)
            return [];

        var blocks = await _blockRepo.GetMasks(new() { Level = filter.Level.ToInt32Parameter(), Chain = filter.Chain });
        
        var events = Data.Models.AllBlockEvents.None;
        var operations = Data.Models.AllOperations.None;
        foreach (var block in blocks)
        {
            events |= block.Events;
            operations |= block.Operations;
        }

        if (events == Data.Models.AllBlockEvents.None && operations == Data.Models.AllOperations.None)
            return [];

        var tasks = new List<Task<IEnumerable<IActivity>>>();

        if (operations.HasFlag(Data.Models.AllOperations.Transaction) && types.Contains(ActivityTypes.Transaction))
            tasks.Add(_transactionRepo.Activity(filter.Level, filter.Chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Reveal) && types.Contains(ActivityTypes.Reveal))
            tasks.Add(_revealRepo.Activity(filter.Level, filter.Chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.IncreasePaidStorage) && types.Contains(ActivityTypes.IncreasePaidStorage))
            tasks.Add(_increasePaidStorageRepo.Activity(filter.Level, filter.Chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.TransferTicket) && types.Contains(ActivityTypes.TransferTicket))
            tasks.Add(_transferTicketRepo.Activity(filter.Level, filter.Chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.RegisterConstant) && types.Contains(ActivityTypes.RegisterConstant))
            tasks.Add(_registerConstantRepo.Activity(filter.Level, filter.Chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Deposit) && types.Contains(ActivityTypes.Deposit))
            tasks.Add(_depositRepo.Activity(filter.Level, filter.Chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Origination) && types.Contains(ActivityTypes.Origination))
            tasks.Add(_originationRepo.Activity(filter.Level, filter.Chain, pagination));

        if (operations.HasFlag(Data.Models.AllOperations.Migration) && types.Contains(ActivityTypes.Migration))
            tasks.Add(_migrationRepo.Activity(filter.Level, filter.Chain, pagination));

        if (events.HasFlag(Data.Models.AllBlockEvents.Tokens) && types.Contains(ActivityTypes.TokenTransfer))
            tasks.Add(_tokenTransferRepo.Activity(filter.Level, filter.Chain, pagination));

        if (events.HasFlag(Data.Models.AllBlockEvents.Tickets) && types.Contains(ActivityTypes.TicketTransfer))
            tasks.Add(_ticketTransferRepo.Activity(filter.Level, filter.Chain, pagination));

        await Task.WhenAll(tasks);

        return Paginate(tasks.SelectMany(x => x.Result), pagination);
    }

    public async Task<IEnumerable<IOpgActivity>> Get(OpgActivityFilter filter, CursorPagination pagination)
    {
        ValidatePagination(pagination);

        var types = filter.Types?.Types ?? ActivityTypes.Default;
        if (types.Count == 0)
            return [];

        var tasks = new List<Task<IEnumerable<IOpgActivity>>>(7)
        {
            Task.FromResult<IEnumerable<IOpgActivity>>([]),
            Task.FromResult<IEnumerable<IOpgActivity>>([]),
            Task.FromResult<IEnumerable<IOpgActivity>>([]),
        };
        var hasTokenTransfers = types.Contains(ActivityTypes.TokenTransfer);
        var hasTicketTransfers = types.Contains(ActivityTypes.TicketTransfer);

        // amend cursor pagination for transfers phase
        var (extPagination, idCursor) = ExtendPagination(pagination);

        if (types.Contains(ActivityTypes.Transaction) || hasTokenTransfers || hasTicketTransfers)
            tasks[0] = _transactionRepo.Activity(filter.Hash, filter.Chain, extPagination);

        if (types.Contains(ActivityTypes.Origination) || hasTokenTransfers)
            tasks[1] = _originationRepo.Activity(filter.Hash, filter.Chain, extPagination);

        if (types.Contains(ActivityTypes.TransferTicket) || hasTicketTransfers)
            tasks[2] = _transferTicketRepo.Activity(filter.Hash, filter.Chain, extPagination);

        if (types.Contains(ActivityTypes.Reveal))
            tasks.Add(_revealRepo.Activity(filter.Hash, filter.Chain, pagination));

        if (types.Contains(ActivityTypes.IncreasePaidStorage))
            tasks.Add(_increasePaidStorageRepo.Activity(filter.Hash, filter.Chain, pagination));

        if (types.Contains(ActivityTypes.RegisterConstant))
            tasks.Add(_registerConstantRepo.Activity(filter.Hash, filter.Chain, pagination));

        if (types.Contains(ActivityTypes.Deposit))
            tasks.Add(_depositRepo.Activity(filter.Hash, filter.Chain, pagination));

        await Task.WhenAll(tasks);

        if (hasTokenTransfers || hasTicketTransfers)
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

            if (!types.Contains(ActivityTypes.Transaction)) tasks[0] = Task.FromResult<IEnumerable<IOpgActivity>>([]);
            if (!types.Contains(ActivityTypes.Origination)) tasks[1] = Task.FromResult<IEnumerable<IOpgActivity>>([]);
            if (!types.Contains(ActivityTypes.TransferTicket)) tasks[2] = Task.FromResult<IEnumerable<IOpgActivity>>([]);

            await Task.WhenAll(tasks);
        }

        var items = tasks.SelectMany(x => x.Result);
        if (idCursor is long id)
            items = items.Where(x => x.Id > id);

        return Paginate(items, pagination);
    }

    static void ValidatePagination(CursorPagination pagination)
    {
        if (pagination.Sort == null)
            pagination.Sort = new() { Cols = [("id", true)] };
        else if (pagination.Sort.Cols.Any(x => x.field != "timestamp" && x.field != "id"))
            throw new BadRequestException(nameof(pagination.Sort), "This endpoint allows sorting by 'timestamp' and/or 'id' only.");
    }

    static IEnumerable<T> Paginate<T>(IEnumerable<T> items, CursorPagination pagination) where T : IActivity
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

        if (!sort.Cols.Any(x => x.field == "id"))
        {
            result = sort.Cols[^1].asc
                ? result.ThenBy(x => x.Id)
                : result.ThenByDescending(x => x.Id);
        }

        return result.Take(pagination.Limit);
    }
    
    static (CursorPagination, long?) ExtendPagination(CursorPagination pagination)
    {
        if (pagination.Cursor?.Cols?.Count > 0)
        {
            var cols = Math.Min(pagination.Sort!.Cols.Count, pagination.Cursor.Cols.Count);
            for (int i = 0; i < cols; i++)
            {
                var (field, asc) = pagination.Sort.Cols[i];
                if (field == "id")
                {
                    // desc sort doesn't need extension
                    if (!asc) break;

                    if (long.TryParse(pagination.Cursor.Cols[i], out var id) && id >= 0)
                    {
                        var newCursor = new CursorParameter { Cols = [.. pagination.Cursor.Cols] };
                        newCursor.Cols[i] = ((id & ~0xFFFFFL) - 1).ToString();

                        var newPagination = new CursorPagination
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
