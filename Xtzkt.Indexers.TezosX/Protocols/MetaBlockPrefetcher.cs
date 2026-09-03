using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols;

class MetaBlockPrefetcher(IHelpers helpers, int depth, int targetLevel)
{
    readonly IHelpers _helpers = helpers;
    readonly int _depth = depth;
    readonly int _targetLevel = targetLevel;
    readonly Dictionary<int, Task<MetaBlock>> _buffer = new(depth + 1);

    public Task<MetaBlock> GetMetaBlock(int level)
    {
        // blocks are always requested one after another, so the buffer shouldn't bloat
        if (!_buffer.Remove(level, out var task))
            task = StartFetch(level);

        for (var next = level + 1; next <= Math.Min(_targetLevel, level + _depth); next++)
            if (!_buffer.ContainsKey(next))
                _buffer.Add(next, StartFetch(next));

        return task;
    }

    public async Task DrainAsync()
    {
        if (_buffer.Count == 0)
            return;

        try { await Task.WhenAll(_buffer.Values.ToArray()); }
        catch { /* fetches are discarded, so their failures don't matter */ }
        
        _buffer.Clear();
    }

    Task<MetaBlock> StartFetch(int level)
    {
        return Task.Run(() => _helpers.GetMetaBlock(level));
    }
}
