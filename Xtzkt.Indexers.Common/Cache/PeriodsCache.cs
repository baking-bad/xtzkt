using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class PeriodsCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<int, VotingPeriod> Cached = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 32;
        TargetCap = size?.TargetCap ?? 16;
        Cached = new(SoftCap + 5);
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

    public void Add(VotingPeriod period)
    {
        Cached[period.Index] = period;
    }

    public void Remove(VotingPeriod period)
    {
        Cached.Remove(period.Index);
    }

    public async Task<VotingPeriod> GetAsync(int index)
    {
        if (!Cached.TryGetValue(index, out var period))
        {
            period = await Db.VotingPeriods.FirstOrDefaultAsync(x => x.ChainId == Chain.Id && x.Index == index)
                ?? throw new Exception($"Voting period #{index} not found");

            Add(period);
        }

        return period;
    }
}
