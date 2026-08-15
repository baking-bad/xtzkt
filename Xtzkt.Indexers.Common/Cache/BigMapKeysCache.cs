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
    static Dictionary<(int, string), BigMapKey> Cached = [];

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

    public async Task Prefetch(IEnumerable<(int id, string hash)> keys)
    {
        var missed = keys.Where(x => !Cached.ContainsKey((x.id, x.hash))).ToHashSet();
        if (missed.Count != 0)
        {
            for (int i = 0, n = 2048; i < missed.Count; i += n)
            {
                var idHashes = string.Join(',', missed.Skip(i).Take(n).Select(x => $"({x.id}, '{x.hash}')")); // TODO: use parameters
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
                var loaded = await Db.BigMapKeys
                    .FromSqlRaw($"""
                        SELECT *
                        FROM "BigMapKeys"
                        WHERE ("BigMapId", "KeyHash") IN ({idHashes})
                        """)
                    .ToListAsync();
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

                foreach (var item in loaded)
                    Cached.Add((item.BigMapId, item.KeyHash), item);
            }
        }
    }

    public bool TryGet(int id, string hash, [NotNullWhen(true)] out BigMapKey? key)
    {
        return Cached.TryGetValue((id, hash), out key);
    }
}
