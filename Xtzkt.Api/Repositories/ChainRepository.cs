using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Models;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Services.Cache;

namespace Xtzkt.Api.Repositories;

public class ChainRepository(ChainCache _chainCache)
{
    public const string SortField = "id";

    IEnumerable<Data.Models.Chain> FromCache(ChainFilter filter)
    {
        return _chainCache.Get().Where(x =>
            (filter.Id?.Matches(x.Id) ?? true) &&
            (filter.ChainId?.Matches(x.ChainId) ?? true) &&
            (filter.Layer?.Matches((int)x.Layer) ?? true));
    }

    List<Data.Models.Chain> Query(ChainFilter filter, Pagination pagination)
    {
        #region sort
        var asc = true;
        if (pagination.Sort?.Cols.Count > 0)
        {
            foreach (var (field, _) in pagination.Sort.Cols)
                if (field != SortField)
                    throw new BadRequestException(nameof(Pagination.Sort), $"Sort by {field} is not allowed. Allowed fields: {SortField}");

            asc = pagination.Sort.Cols[0].asc;
        }
        #endregion

        #region cursor
        int? cursor = null;
        if (pagination.Cursor?.Cols?.Count > 0)
        {
            if (pagination.Cursor.Cols.Count > 1)
                throw new BadRequestException(nameof(Pagination.Cursor), "Cursor must match sort");

            if (!int.TryParse(pagination.Cursor.Cols[0], out var value))
                throw new BadRequestException(nameof(Pagination.Cursor), "Invalid cursor value");

            cursor = value;
        }
        #endregion

        var res = FromCache(filter);

        if (cursor is int id)
            res = asc ? res.Where(x => x.Id > id) : res.Where(x => x.Id < id);

        res = asc ? res.OrderBy(x => x.Id) : res.OrderByDescending(x => x.Id);

        return [.. res.Skip(pagination.Offset).Take(pagination.Limit)];
    }

    public long Count(ChainFilter filter)
    {
        return FromCache(filter).Count();
    }

    public IEnumerable<Chain> Get(ChainFilter filter, Pagination pagination)
    {
        return Query(filter, pagination).Select<Data.Models.Chain, Chain>(chain => chain switch
        {
            Data.Models.L1Chain l1 => new L1Chain
            {
                Id = l1.Id,
                ChainId = l1.ChainId,
                Network = l1.Network,
                Hash = l1.Hash,
                Level = l1.Level,
                Timestamp = l1.Timestamp,
                KnownLevel = l1.KnownLevel,
                SyncedAt = l1.SyncedAt,
                Cycle = l1.Cycle,
                NextProtocol = l1.NextProtocol,
                Protocol = l1.Protocol,
                VotingEpoch = l1.VotingEpoch,
                VotingPeriod = l1.VotingPeriod,
            },
            Data.Models.XChain x => new XChain
            {
                Id = x.Id,
                ChainId = x.ChainId,
                Network = x.Network,
                Hash = x.Hash,
                Level = x.Level,
                Timestamp = x.Timestamp,
                KnownLevel = x.KnownLevel,
                SyncedAt = x.SyncedAt,
                Kernel = x.Kernel,
                KernelUpgrade = x.KernelUpgrade,
                KernelUpgradeTime = x.KernelUpgradeTime,
                MichelsonActivationLevel = x.MichelsonActivationLevel,
                MichelsonBlock = x.MichelsonBlock,
                MichelsonChainId = x.MichelsonChainId,
                MichelsonProtocol = x.MichelsonProtocol,
                RollupAddress = x.RollupAddress,
            },
            _ => throw new InvalidOperationException("Failed to read Chain")
        });
    }

    public object?[][] Get(ChainFilter filter, Pagination pagination, Selection selection)
    {
        var rows = Query(filter, pagination);

        var fields = selection.Fields();
        var result = new object?[rows.Count][];
        for (int i = 0; i < result.Length; i++)
            result[i] = new object?[fields.Count];

        for (int i = 0, j = 0; i < fields.Count; j = 0, i++)
        {
            switch (fields[i].Full)
            {
                case "layer":
                    foreach (var row in rows) result[j++][i] = Layers.ToString((int)row.Layer);
                    break;
                // Chain
                case "id":
                    foreach (var row in rows) result[j++][i] = row.Id;
                    break;
                case "chainId":
                    foreach (var row in rows) result[j++][i] = row.ChainId;
                    break;
                case "network":
                    foreach (var row in rows) result[j++][i] = row.Network;
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
                case "knownLevel":
                    foreach (var row in rows) result[j++][i] = row.KnownLevel;
                    break;
                case "syncedAt":
                    foreach (var row in rows) result[j++][i] = row.SyncedAt;
                    break;
                // XChain
                case "rollupAddress":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.RollupAddress;
                    break;
                case "kernel":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.Kernel;
                    break;
                case "kernelUpgrade":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.KernelUpgrade;
                    break;
                case "kernelUpgradeTime":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.KernelUpgradeTime;
                    break;
                case "michelsonActivationLevel":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.MichelsonActivationLevel;
                    break;
                case "michelsonChainId":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.MichelsonChainId;
                    break;
                case "michelsonProtocol":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.MichelsonProtocol;
                    break;
                case "michelsonBlock":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.XChain)?.MichelsonBlock;
                    break;
                // L1Chain
                case "cycle":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.L1Chain)?.Cycle;
                    break;
                case "protocol":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.L1Chain)?.Protocol;
                    break;
                case "nextProtocol":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.L1Chain)?.NextProtocol;
                    break;
                case "votingEpoch":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.L1Chain)?.VotingEpoch;
                    break;
                case "votingPeriod":
                    foreach (var row in rows) result[j++][i] = (row as Data.Models.L1Chain)?.VotingPeriod;
                    break;
                default: throw new BadRequestException(nameof(selection.Select), $"Field {fields[i].Full} doesn't exist");
            }
        }

        return result;
    }
}
