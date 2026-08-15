using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class SoftwareCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<int, Software> CachedById = [];
    static Dictionary<string, Software> CachedByHash = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 64;
        TargetCap = size?.TargetCap ?? 32;
        CachedById = new(SoftCap + 3);
        CachedByHash = new(SoftCap + 3);
    }
    #endregion

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public void Reset()
    {
        CachedById.Clear();
        CachedByHash.Clear();
    }

    public void Trim()
    {
        if (CachedById.Count > SoftCap)
        {
            var toRemove = CachedById.Values
                .OrderBy(x => x.LastLevel)
                .Take(CachedById.Count - TargetCap)
                .ToList();

            foreach (var item in toRemove)
                Remove(item);
        }
    }

    public void Add(Software item)
    {
        CachedById[item.Id] = item;
        CachedByHash[item.ShortHash] = item;
    }

    public void Remove(Software item)
    {
        CachedById.Remove(item.Id);
        CachedByHash.Remove(item.ShortHash);
    }

    public async Task<Software> GetAsync(int id)
    {
        if (!CachedById.TryGetValue(id, out var item))
        {
            item = await Db.Software.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new Exception($"Software #{id} doesn't exist");

            Add(item);
        }

        return item;
    }

    public async Task<Software> GetOrCreateAsync(string hash, Func<Software> create)
    {
        if (!CachedByHash.TryGetValue(hash, out var item))
        {
            item = await Db.Software.FirstOrDefaultAsync(x => x.ChainId == Chain.Id && x.ShortHash == hash)
                ?? create();

            Add(item);
        }

        return item;
    }
}
