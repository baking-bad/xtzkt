using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class L1StatisticsCache(XtzktContext db, ChainConfig chain)
{
    static L1Statistics? _Current;

    public L1Statistics Current => _Current!;

    public L1Statistics? CurrentOr => _Current;

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public async Task ResetAsync()
    {
        _Current = await Db.Statistics
            .OfType<L1Statistics>()
            .Where(x => x.ChainId == Chain.Id)
            .OrderByDescending(x => x.Level)
            .FirstOrDefaultAsync();
    }

    public void SetCurrent(L1Statistics stats)
    {
        _Current = stats;
    }
}
