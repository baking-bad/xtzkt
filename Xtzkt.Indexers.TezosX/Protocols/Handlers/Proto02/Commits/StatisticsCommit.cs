using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class StatisticsCommit(ProtocolHandler protocol) : Proto02Commit(protocol)
{
    public virtual async Task Apply()
    {
        if (Cache.Statistics.CurrentOr is XStatistics prev)
        {
            var timestamp = Context.Block.Timestamp;
            var prevTimestamp = (await Cache.Blocks.GetAsync(prev.Level)).Timestamp;
            if (timestamp.Ticks / (10_000_000L * 3600 * 24) != prevTimestamp.Ticks / (10_000_000L * 3600 * 24))
            {
                Db.TryAttach(prev);
                prev.Date = prevTimestamp.Date;
            }
        }

        Cache.Statistics.SetCurrent(Context.Statistics);
        Db.Statistics.Add(Context.Statistics);
    }

    public virtual async Task Revert()
    {
        await Cache.Statistics.ResetAsync();
        await Db.Statistics
            .Where(x => x.ChainId == Context.Block.ChainId && x.Level == Context.Block.Level)
            .ExecuteDeleteAsync();
    }
}
