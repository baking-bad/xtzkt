using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class XStatisticsCache(XtzktContext db, ChainConfig chain)
{
    static XStatistics? _Current;

    public XStatistics Current => _Current!;

    public XStatistics? CurrentOr => _Current;

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public async Task ResetAsync()
    {
        _Current = await Db.Statistics
            .OfType<XStatistics>()
            .Where(x => x.ChainId == Chain.Id)
            .OrderByDescending(x => x.Level)
            .FirstOrDefaultAsync();
    }

    public void SetCurrent(XStatistics stats)
    {
        _Current = stats;
    }
}
