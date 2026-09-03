using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class BigMapKeysCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<(int, HashKey), BigMapKey> Cached = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 120_000;
        TargetCap = size?.TargetCap ?? 100_000;
        Cached = new(SoftCap + 16_384);
    }
    #endregion

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public void Reset()
    {
        Cached.Clear();
    }

    public void Trim()
    {
        if (Cached.Count > SoftCap)
        {
            var toRemove = Cached.Values
                .OrderBy(x => x.LastLevel)
                .Take(Cached.Count - TargetCap)
                .ToList();

            foreach (var item in toRemove)
                Remove(item);
        }
    }

    public void Add(BigMapKey key)
    {
        Cached[(key.BigMapId, key.KeyHash)] = key;
    }

    public void Add(IEnumerable<BigMapKey> keys)
    {
        foreach (var key in keys)
            Cached[(key.BigMapId, key.KeyHash)] = key;
    }

    public void Remove(BigMapKey key)
    {
        Cached.Remove((key.BigMapId, key.KeyHash));
    }

    public async Task Prefetch(IEnumerable<(int id, byte[] hash)> keys)
    {
        var missed = keys
            .Where(x => !Cached.ContainsKey((x.id, x.hash)))
            .Select(x => (x.id, hash: (HashKey)x.hash))
            .Distinct()
            .ToList();

        if (missed.Count != 0)
        {
            var ids = missed.Select(x => x.id).ToArray();
            var hashes = missed.Select(x => x.hash.Bytes).ToArray();

            var items = await Db.BigMapKeys
                .FromSqlRaw("""
                    SELECT k.*
                    FROM unnest({0}::int[], {1}::bytea[]) AS q(id, hash)
                    INNER JOIN "BigMapKeys" k ON k."BigMapId" = q.id AND k."KeyHash" = q.hash
                    """, ids, hashes)
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }

    public bool TryGet(int id, byte[] hash, [NotNullWhen(true)] out BigMapKey? key)
    {
        return Cached.TryGetValue((id, hash), out key);
    }
}
