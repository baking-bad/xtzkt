using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class BlocksCache(XtzktContext db, IChainCache chain, ChainConfig chainConfig)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<int, L1Block> Cached = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 64_000;
        TargetCap = size?.TargetCap ?? 32_000;
        Cached = new(SoftCap + 256);
    }
    #endregion

    readonly XtzktContext Db = db;
    readonly IChainCache Chain = chain;
    readonly ChainConfig ChainConfig = chainConfig;

    public void Reset()
    {
        Cached.Clear();
    }

    public void Trim()
    {
        if (Cached.Count > SoftCap)
        {
            var toRemove = Cached.Values
                .OrderBy(x => x.Level)
                .Take(Cached.Count - TargetCap)
                .ToList();

            foreach (var item in toRemove)
                Remove(item);
        }
    }

    public void Add(L1Block block)
    {
        Cached[block.Level] = block;
    }

    public void Remove(L1Block block)
    {
        Cached.Remove(block.Level);
    }

    public async Task Preload(IEnumerable<int> levels)
    {
        var missed = levels.Where(x => !Cached.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            var items = await Db.L1Blocks
                .Where(x => x.ChainId == ChainConfig.Id && missed.Contains(x.Level))
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }

    public L1Block Current()
    {
        return Get(Chain.GetLevel());
    }

    public Task<L1Block> CurrentAsync()
    {
        return GetAsync(Chain.GetLevel());
    }

    public Task<L1Block> PreviousAsync()
    {
        return GetAsync(Chain.GetLevel() - 1);
    }

    public L1Block Get(int level)
    {
        if (!Cached.TryGetValue(level, out var block))
        {
            block = Db.L1Blocks.FirstOrDefault(x => x.ChainId == ChainConfig.Id && x.Level == level)
                ?? throw new Exception($"Block #{level} doesn't exist");

            Add(block);
        }

        return block;
    }

    public async Task<L1Block> GetAsync(int level)
    {
        if (!Cached.TryGetValue(level, out var block))
        {
            block = await Db.L1Blocks.FirstOrDefaultAsync(x => x.ChainId == ChainConfig.Id && x.Level == level)
                ?? throw new Exception($"Block #{level} doesn't exist");

            Add(block);
        }

        return block;
    }

    public L1Block GetCached(int level)
    {
        if (!Cached.TryGetValue(level, out var block))
            throw new Exception($"Block #{level} is not cached");

        return block;
    }
}
