using System.Collections.Concurrent;
using Dapper;
using Npgsql;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class BlockCache(ChainCache _chainCache, NpgsqlDataSource _dataSource)
{
    #region cache
    const int CacheSize = 1 << 16;
    const int VolatileWindow = 60;

    readonly ConcurrentDictionary<(int ChainId, int Level), (long, long)> LevelIds = new();
    readonly ConcurrentDictionary<(int ChainId, DateTime Timestamp), (long, long, bool)> TimestampIds = new();

    readonly ConcurrentDictionary<(int ChainId, int Level), Task<(long, long)?>> LevelIdRequests = new();
    readonly ConcurrentDictionary<(int ChainId, DateTime Timestamp), Task<(long From, long To, bool Exact, int Level)?>> TimestampIdRequests = new();

    async Task<(long, long)?> GetBlockRange(Chain chain, int level)
    {
        if (level < chain.GenesisLevel)
            return null;

        if (LevelIds.TryGetValue((chain.Id, level), out var cached))
            return cached;

        var range = await Dedup(LevelIdRequests, (chain.Id, level), async _ =>
        {
            await using var db = await _dataSource.OpenConnectionAsync();
            var rows = await db.QueryAsync<long>("""
                SELECT "Id"
                FROM "Blocks"
                WHERE "ChainId" = @chainId AND "Level" >= @level
                ORDER BY "Level"
                LIMIT 2
                """, new { chainId = chain.Id, level });

            return rows.Count() switch
            {
                0 => null,
                1 => (rows.First(), IdLayout.MaxId64(chain.Id)),
                _ => (rows.First(), rows.Last() - 1),
            };
        });

        if (range != null && level <= chain.Level - VolatileWindow)
        {
            if (LevelIds.Count > CacheSize)
                LevelIds.Clear();

            LevelIds.TryAdd((chain.Id, level), range.Value);
        }

        return range;
    }

    async Task<(long, long, bool)?> GetBlockRange(Chain chain, DateTime timestamp)
    {
        if (timestamp < chain.GenesisTimestamp)
            return null;

        if (TimestampIds.TryGetValue((chain.Id, timestamp), out var cached))
            return cached;

        var block = await Dedup(TimestampIdRequests, (chain.Id, timestamp), async _ =>
        {
            await using var db = await _dataSource.OpenConnectionAsync();
            var row = await db.QueryFirstOrDefaultAsync($"""
                SELECT a."Id" AS from, b."Id" AS to, a."Timestamp" = @timestamp AS exact, a."Level" AS level
                FROM "Blocks" AS a
                LEFT JOIN "Blocks" AS b ON b."ChainId" = a."ChainId" AND b."Level" = a."Level" + 1
                WHERE a."ChainId" = @chainId AND a."Timestamp" <= @timestamp
                ORDER BY a."Timestamp" DESC
                LIMIT 1
                """, new { chainId = chain.Id, timestamp });

            if (row == null)
                return ((long, long, bool, int)?)null;

            if (row.to == null)
                return ((long)row.from, IdLayout.MaxId64(chain.Id), (bool)row.exact, (int)row.level);

            return ((long)row.from, (long)row.to - 1, (bool)row.exact, (int)row.level);
        });

        if (block is not var (from, to, exact, level))
            return null;

        if (level <= chain.Level - VolatileWindow)
        {
            if (TimestampIds.Count > CacheSize)
                TimestampIds.Clear();

            TimestampIds.TryAdd((chain.Id, timestamp), (from, to, exact));
        }

        return (from, to, exact);
    }

    static async Task<TValue> Dedup<TKey, TValue>(ConcurrentDictionary<TKey, Task<TValue>> requests, TKey key, Func<TKey, Task<TValue>> load) where TKey : notnull
    {
        var task = requests.GetOrAdd(key, load);
        try
        {
            return await task;
        }
        finally
        {
            // by value, so a late awaiter can't evict a newer request that has already replaced this one
            requests.TryRemove(new KeyValuePair<TKey, Task<TValue>>(key, task));
        }
    }
    #endregion

    #region resolvers
    public bool TryResolveLevel(Int32EqParameter p, Chain chain, out int level)
    {
        if (p.Eq is not int eq || eq < chain.GenesisLevel)
        {
            level = -1;
            return false;
        }

        level = eq;
        return true;
    }

    public IdRange? ReduceRange(IdRange range, Int64Parameter id)
    {
        var (min, max) = range;

        if (id.Eq is long eq)
        {
            if (eq < min || eq > max)
                return null;

            min = eq;
            max = eq;
        }

        if (id.In is List<long> @in)
        {
            var _min = @in.Min();
            var _max = @in.Max();

            if (_max < min || _min > max)
                return null;

            min = Math.Max(min, _min);
            max = Math.Min(max, _max);
        }

        if (id.Gt is long gt)
        {
            if (gt >= max)
                return null;

            min = Math.Max(min, gt + 1);
        }

        if (id.Ge is long ge)
        {
            if (ge > max)
                return null;

            min = Math.Max(min, ge);
        }

        if (id.Lt is long lt)
        {
            if (lt <= min)
                return null;

            max = Math.Min(max, lt - 1);
        }

        if (id.Le is long le)
        {
            if (le < min)
                return null;

            max = Math.Min(max, le);
        }

        return new(min, max);
    }

    public IdRange? ReduceRange(IdRange range, long id, bool asc)
    {
        var (min, max) = range;

        if (asc)
        {
            if (id > max)
                return null;

            min = Math.Max(min, id);
        }
        else
        {
            if (id < min)
                return null;

            max = Math.Min(max, id);
        }

        return new(min, max);
    }

    public async Task<IdRange?> ReduceRange(IdRange range, Chain chain, Int32RangeParameter level)
    {
        var (min, max) = range;

        if (level.Eq is int eq)
        {
            if (await GetBlockRange(chain, eq) is not var (firstId, lastId))
                return null;

            if (lastId < min || firstId > max)
                return null;

            min = Math.Max(min, firstId);
            max = Math.Min(max, lastId);
        }

        if (level.Gt is int gt && gt >= chain.GenesisLevel)
        {
            if (await GetBlockRange(chain, gt) is not var (_, lastId))
                return null;

            if (lastId >= max)
                return null;

            min = Math.Max(min, lastId + 1);
        }

        if (level.Ge is int ge && ge > chain.GenesisLevel)
        {
            if (await GetBlockRange(chain, ge) is not var (firstId, _))
                return null;

            if (firstId > max)
                return null;

            min = Math.Max(min, firstId);
        }

        if (level.Lt is int lt && lt <= chain.Level)
        {
            if (lt <= chain.GenesisLevel)
                return null;

            if (await GetBlockRange(chain, lt) is var (firstId, _))
            {
                if (firstId <= min)
                    return null;

                max = Math.Min(max, firstId - 1);
            }
        }

        if (level.Le is int le && le < chain.Level)
        {
            if (le < chain.GenesisLevel)
                return null;

            if (await GetBlockRange(chain, le) is var (_, lastId))
            {
                if (lastId < min)
                    return null;

                max = Math.Min(max, lastId);
            }
        }

        return new(min, max);
    }

    public async Task<IdRange?> ReduceRange(IdRange range, Chain chain, DateTimeRangeParameter timestamp)
    {
        var (min, max) = range;

        if (timestamp.Eq is DateTime eq)
        {
            if (await GetBlockRange(chain, eq) is not var (firstId, lastId, exact) || !exact)
                return null;

            if (lastId < min || firstId > max)
                return null;

            min = Math.Max(min, firstId);
            max = Math.Min(max, lastId);
        }

        if (timestamp.Gt is DateTime gt && gt >= chain.GenesisTimestamp)
        {
            if (await GetBlockRange(chain, gt) is not var (_, lastId, _))
                return null;

            if (lastId >= max)
                return null;

            min = Math.Max(min, lastId + 1);
        }

        if (timestamp.Ge is DateTime ge && ge > chain.GenesisTimestamp)
        {
            if (await GetBlockRange(chain, ge) is not var (firstId, lastId, exact))
                return null;

            if (exact)
            {
                if (firstId > max)
                    return null;

                min = Math.Max(min, firstId);
            }
            else
            {
                if (lastId >= max)
                    return null;

                min = Math.Max(min, lastId + 1);
            }
        }

        if (timestamp.Lt is DateTime lt && lt <= chain.Timestamp)
        {
            if (lt <= chain.GenesisTimestamp)
                return null;

            if (await GetBlockRange(chain, lt) is not var (firstId, lastId, exact))
                return null;

            if (exact)
            {
                if (firstId <= min)
                    return null;

                max = Math.Min(max, firstId - 1);
            }
            else
            {
                if (lastId < min)
                    return null;

                max = Math.Min(max, lastId);
            }
        }

        if (timestamp.Le is DateTime le && le < chain.Timestamp)
        {
            if (le < chain.GenesisTimestamp)
                return null;

            if (await GetBlockRange(chain, le) is not var (_, lastId, _))
                return null;

            if (lastId < min)
                return null;

            max = Math.Min(max, lastId);
        }

        return new(min, max);
    }

    public async Task<IdRange?> ReduceRange(IdRange range, Chain chain, DateTime timestamp, bool asc)
    {
        var (min, max) = range;

        if (asc && timestamp > chain.GenesisTimestamp)
        {
            if (await GetBlockRange(chain, timestamp) is not var (firstId, _, _))
                return null;

            if (firstId > max)
                return null;

            min = Math.Max(min, firstId);
        }

        if (!asc && timestamp < chain.Timestamp)
        {
            if (timestamp < chain.GenesisTimestamp)
                return null;

            if (await GetBlockRange(chain, timestamp) is not var (_, lastId, _))
                return null;

            if (lastId < min)
                return null;

            max = Math.Min(max, lastId);
        }

        return new(min, max);
    }

    public async Task<List<IdRange>?> ProcessOpFilters(
        List<Chain> chains,
        Int64Parameter? id,
        Int32RangeParameter? level,
        DateTimeRangeParameter? timestamp,
        Pagination? pagination)
    {
        #region validate pagination
        // pagination should be already reduced
        if (pagination?.Sort!.Cols is [("timestamp", var asc1), ("id", var asc2)] && asc1 != asc2)
            throw new BadRequestException(nameof(pagination.Sort), "Sorting by different directions is not allowed for this endpoint");
        #endregion

        #region build ranges
        var usedChains = new List<Chain>(chains.Count);
        var ranges = new List<IdRange>(chains.Count);

        foreach (var chain in chains)
        {
            var range = new IdRange(IdLayout.MinId64(chain.Id), IdLayout.MaxId64(chain.Id));

            if (id != null)
            {
                var reducedRange = ReduceRange(range, id);
                if (reducedRange == null) continue;
                range = reducedRange.Value;
            }

            if (level != null)
            {
                var reducedRange = await ReduceRange(range, chain, level);
                if (reducedRange == null) continue;
                range = reducedRange.Value;
            }

            if (timestamp != null)
            {
                var reducedRange = await ReduceRange(range, chain, timestamp);
                if (reducedRange == null) continue;
                range = reducedRange.Value;
            }

            if (pagination?.GetDateTimeCursorOrDefault("timestamp") is var (timestampCursor, timestampAsc))
            {
                var reducedRange = await ReduceRange(range, chain, timestampCursor, timestampAsc);
                if (reducedRange == null) continue;
                range = reducedRange.Value;
            }
            else if (pagination?.GetInt64CursorOrDefault("id") is var (idCursor, idAsc))
            {
                var reducedRange = ReduceRange(range, idCursor, idAsc);
                if (reducedRange == null) continue;
                range = reducedRange.Value;
            }

            usedChains.Add(chain);
            ranges.Add(range);
        }

        if (usedChains.Count == 0)
            return null;
        #endregion

        #region reduce chains
        if (chains.Count != usedChains.Count)
        {
            chains.Clear();
            chains.AddRange(usedChains);
        }
        #endregion

        #region rewrite pagination
        if (pagination?.Sort!.Cols[0].field == "timestamp" && chains.Count == 1)
        {
            if (pagination.Cursor?.Cols.Count == 2)
            {
                pagination.Cursor = new() { Cols = [pagination.Cursor.Cols[1]] };
            }
            else if (pagination.GetDateTimeCursorOrDefault("timestamp") is var (ts, asc))
            {
                if (asc)
                {
                    if (ts < chains[0].GenesisTimestamp)
                        pagination.Cursor = null;
                    else if (await GetBlockRange(chains[0], ts) is var (_, lastId, _))
                        pagination.Cursor = new() { Cols = [lastId.ToString()] };
                    else
                        return null;
                }
                else
                {
                    if (ts < chains[0].GenesisTimestamp)
                        return null;
                    else if (await GetBlockRange(chains[0], ts) is not var (firstId, lastId, exact))
                        return null;
                    else if (exact)
                        pagination.Cursor = new() { Cols = [firstId.ToString()] };
                    else if (lastId == IdLayout.MaxId64(chains[0].Id))
                        pagination.Cursor = null;
                    else
                        pagination.Cursor = new() { Cols = [(lastId + 1).ToString()] };
                }
            }

            pagination.Sort = new() { Cols = [("id", pagination.Sort.Cols[0].asc)] };
        }
        #endregion

        #region merge ranges
        if (pagination?.Sort!.Cols[0].field != "timestamp")
        {
            for (int i = ranges.Count - 1; i >= 1; i--)
            {
                if (ranges[i].Min == ranges[i - 1].Max + 1)
                {
                    ranges[i - 1] = new(ranges[i - 1].Min, ranges[i].Max);
                    ranges.RemoveAt(i);
                }
            }

            if (ranges.Count == 1 &&
                ranges[0].Min == IdLayout.MinId64(chains[0].Id) &&
                ranges[0].Max == IdLayout.MaxId64(chains[^1].Id) &&
                chains.Count == _chainCache.Count())
                ranges.Clear();
        }
        #endregion

        return ranges;
    }
    #endregion
}
