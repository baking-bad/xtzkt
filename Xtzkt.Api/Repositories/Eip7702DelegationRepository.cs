using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Repositories;

public class Eip7702DelegationRepository(
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

    async Task<bool> ProcessFilters(Eip7702DelegationFilter filter)
    {
        filter.Chain = _chainCache.ResolveChainFilter(filter.Chain);
        var chainId = filter.Chain.Id!.Eq;

        if (chainId == -1)
            return false;

        if (filter.Sender?.Hash != null)
            filter.Sender.Id += await filter.Sender.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Authority?.Hash != null)
            filter.Authority.Id += await filter.Authority.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.PrevDelegate?.Hash != null)
            filter.PrevDelegate.Id += await filter.PrevDelegate.Hash.ToIdParameter(_addressCache, chainId);

        if (filter.Delegate?.Hash != null)
            filter.Delegate.Id += await filter.Delegate.Hash.ToIdParameter(_addressCache, chainId);

        return true;
    }

    async Task<IEnumerable<dynamic>> Query(Eip7702DelegationFilter filter, Pagination pagination, Selection? selection = null)
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
                    case "id":            columns.Add(@"""Id"""); break;
                    case "chain":         columns.Add(@"""ChainId"""); break;
                    case "level":         columns.Add(@"""Level"""); break;
                    case "timestamp":     columns.Add(@"""Timestamp"""); break;
                    case "transactionId": columns.Add(@"""TransactionId"""); break;
                    case "sender":        columns.Add(@"""SenderId"""); break;
                    case "authority":     columns.Add(@"""AuthorityId"""); break;
                    case "nonce":         columns.Add(@"""Nonce"""); break;
                    case "prevDelegate":  columns.Add(@"""PrevDelegateId"""); break;
                    case "delegate":      columns.Add(@"""DelegateId"""); break;
                    default: throw new BadRequestException(nameof(selection.Select), $"Field {field.Field} doesn't exist");
                }
            }
        }

        var (query, parameters) = new SqlBuilder()
            .Select(columns)
            .From(@"""Eip7702Delegations""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""Level""",          filter.Level)
            .Where(@"""Timestamp""",      filter.Timestamp)
            .Where(@"""TransactionId""",  filter.TransactionId)
            .Where(@"""SenderId""",       filter.Sender?.Id)
            .Where(@"""AuthorityId""",    filter.Authority?.Id)
            .Where(@"""Nonce""",          filter.Nonce)
            .Where(@"""PrevDelegateId""", filter.PrevDelegate?.Id)
            .Where(@"""DelegateId""",     filter.Delegate?.Id)
            .OrderBy(pagination.Sort, SortSpec)
            .Cursor(pagination.Cursor, SortSpec)
            .Offset(pagination.Offset)
            .Limit(pagination.Limit)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryAsync(query, parameters);
    }

    public async Task<long> Count(Eip7702DelegationFilter filter)
    {
        if (filter.IsEmpty())
            return _chainCache.Get().OfType<Data.Models.XChain>().Sum(x => x.Eip7702DelegationCount);

        if (!await ProcessFilters(filter))
            return 0;

        var (query, parameters) = new SqlBuilder()
            .Select("COUNT(*)")
            .From(@"""Eip7702Delegations""")
            .Where(@"""Id""",             filter.Id)
            .Where(@"""ChainId""",        filter.Chain?.Id)
            .Where(@"""Level""",          filter.Level)
            .Where(@"""Timestamp""",      filter.Timestamp)
            .Where(@"""TransactionId""",  filter.TransactionId)
            .Where(@"""SenderId""",       filter.Sender?.Id)
            .Where(@"""AuthorityId""",    filter.Authority?.Id)
            .Where(@"""Nonce""",          filter.Nonce)
            .Where(@"""PrevDelegateId""", filter.PrevDelegate?.Id)
            .Where(@"""DelegateId""",     filter.Delegate?.Id)
            .Build();

        await using var db = await _dataSource.OpenConnectionAsync();
        return await db.QueryFirstAsync<long>(query, parameters);
    }

    public async Task<IEnumerable<Eip7702Delegation>> Get(Eip7702DelegationFilter filter, Pagination pagination)
    {
        var rows = await Query(filter, pagination);
        return rows.Select(row => new Eip7702Delegation
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = row.Level,
            Timestamp = row.Timestamp,
            TransactionId = row.TransactionId,
            Sender = _addressCache.GetInfo((int)row.SenderId),
            Authority = _addressCache.GetInfo((int)row.AuthorityId),
            Nonce = row.Nonce,
            PrevDelegate = _addressCache.GetInfo((int?)row.PrevDelegateId),
            Delegate = _addressCache.GetInfo((int?)row.DelegateId),
        });
    }

    public async Task<object?[][]> Get(Eip7702DelegationFilter filter, Pagination pagination, Selection selection)
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
                case "transactionId":
                    foreach (var row in rows) result[j++][i] = row.TransactionId?.ToString();
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
                case "authority":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int)row.AuthorityId);
                    break;
                case "authority.id":
                    foreach (var row in rows) result[j++][i] = row.AuthorityId;
                    break;
                case "authority.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AuthorityId)).Hash;
                    break;
                case "authority.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AuthorityId)).Type;
                    break;
                case "authority.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int)row.AuthorityId)).Alias;
                    break;
                case "nonce":
                    foreach (var row in rows) result[j++][i] = row.Nonce;
                    break;
                case "prevDelegate":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.PrevDelegateId);
                    break;
                case "prevDelegate.id":
                    foreach (var row in rows) result[j++][i] = row.PrevDelegateId;
                    break;
                case "prevDelegate.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.PrevDelegateId))?.Hash;
                    break;
                case "prevDelegate.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.PrevDelegateId))?.Type;
                    break;
                case "prevDelegate.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.PrevDelegateId))?.Alias;
                    break;
                case "delegate":
                    foreach (var row in rows) result[j++][i] = await _addressCache.GetInfoAsync((int?)row.DelegateId);
                    break;
                case "delegate.id":
                    foreach (var row in rows) result[j++][i] = row.DelegateId;
                    break;
                case "delegate.hash":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.DelegateId))?.Hash;
                    break;
                case "delegate.type":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.DelegateId))?.Type;
                    break;
                case "delegate.alias":
                    foreach (var row in rows) result[j++][i] = (await _addressCache.GetInfoAsync((int?)row.DelegateId))?.Alias;
                    break;
            }
        }

        return result;
    }
}
