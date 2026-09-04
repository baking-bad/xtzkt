using System.Text.Json;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols;

class MetaBlockPrefetcher(IHelpers helpers, int depth, int targetLevel)
{
    readonly IHelpers _helpers = helpers;
    readonly int _depth = depth;
    readonly int _targetLevel = targetLevel;
    readonly Dictionary<int, (Task<JsonElement> Blueprint, Task<MetaBlock> MetaBlock)> _buffer = new(depth + 1);

    public Task<JsonElement> GetBlueprint(int level)
    {
        if (!_buffer.TryGetValue(level, out var tasks))
            _buffer.Add(level, tasks = StartFetch(level));

        StartPrefetch(level);

        return tasks.Blueprint;
    }

    public Task<MetaBlock> GetMetaBlock(int level)
    {
        // blocks are always requested one after another, so the buffer shouldn't bloat
        if (!_buffer.Remove(level, out var tasks))
            tasks = StartFetch(level);

        StartPrefetch(level);

        return tasks.MetaBlock;
    }

    public async Task DrainAsync()
    {
        if (_buffer.Count == 0)
            return;

        // MetaBlock waits for Blueprint under the hood, so we don't need to wait for both
        try { await Task.WhenAll(_buffer.Values.Select(t => t.MetaBlock).ToArray()); }
        catch { /* fetches are discarded, so their failures don't matter */ }
        
        _buffer.Clear();
    }

    void StartPrefetch(int level)
    {
        for (var next = level + 1; next <= Math.Min(_targetLevel, level + _depth); next++)
            if (!_buffer.ContainsKey(next))
                _buffer.Add(next, StartFetch(next));
    }

    (Task<JsonElement>, Task<MetaBlock>) StartFetch(int level)
    {
        var blueprintTask = Task.Run(() => _helpers.GetRawBlueprint(level));
        var metaBlockTask = Task.Run(() => _helpers.GetMetaBlock(level, blueprintTask));
        return (blueprintTask, metaBlockTask);
    }
}
