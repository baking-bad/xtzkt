using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.Common.Cache;

public class SmartRollupCommitmentCache(XtzktContext db)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<int, SmartRollupCommitment> CachedById = [];
    static Dictionary<(HashKey, int), SmartRollupCommitment> CachedByKey = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 10_000;
        TargetCap = size?.TargetCap ?? 5000;
        CachedById = new(SoftCap + 512);
        CachedByKey = new(SoftCap + 512);
    }
    #endregion

    readonly XtzktContext Db = db;

    public void Reset()
    {
        CachedById.Clear();
        CachedByKey.Clear();
    }

    public void Trim()
    {
        if (CachedByKey.Count > SoftCap)
        {
            var toRemove = CachedByKey.Values
                .OrderBy(x => x.LastLevel)
                .Take(CachedByKey.Count - TargetCap)
                .ToList();

            foreach (var item in toRemove)
                Remove(item);
        }
    }

    public void Add(SmartRollupCommitment item)
    {
        CachedById[item.Id] = item;
        CachedByKey[(item.Hash, item.SmartRollupId)] = item;
    }

    public void Remove(SmartRollupCommitment item)
    {
        CachedById.Remove(item.Id);
        CachedByKey.Remove((item.Hash, item.SmartRollupId));
    }

    public async Task<SmartRollupCommitment> GetAsync(int id)
    {
        if (!CachedById.TryGetValue(id, out var item))
        {
            item = await Db.SmartRollupCommitments.SingleOrDefaultAsync(x => x.Id == id)
                ?? throw new Exception($"Smart rollup commitment #{id} doesn't exist");
            Add(item);
        }
        return item;
    }

    public async Task<SmartRollupCommitment> GetAsync(byte[] hash, int rollupId)
    {
        if (!CachedByKey.TryGetValue((hash, rollupId), out var item))
        {
            item = await Db.SmartRollupCommitments
                .Where(x => x.SmartRollupId == rollupId && x.Hash == hash)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync()
                ?? throw new Exception($"Smart rollup commitment ({Hex.GetStringRaw(hash)}, {rollupId}) doesn't exist");
            Add(item);
        }
        return item;
    }

    public async Task<SmartRollupCommitment?> GetOrDefaultAsync(int? id)
    {
        if (id is not int _id)
            return null;

        if (!CachedById.TryGetValue(_id, out var item))
        {
            item = await Db.SmartRollupCommitments.SingleOrDefaultAsync(x => x.Id == _id)
                ?? throw new Exception($"Smart rollup commitment #{_id} doesn't exist");
            Add(item);
        }
        return item;
    }

    public async Task<SmartRollupCommitment?> GetOrDefaultAsync(byte[]? hash, int? rollupId)
    {
        if (hash is not byte[] _hash || rollupId is not int _rollupId)
            return null;

        if (!CachedByKey.TryGetValue((_hash, _rollupId), out var item))
        {
            item = await Db.SmartRollupCommitments
                .Where(x => x.SmartRollupId == _rollupId && x.Hash == _hash)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (item != null) Add(item);
        }
        
        return item;
    }
}
