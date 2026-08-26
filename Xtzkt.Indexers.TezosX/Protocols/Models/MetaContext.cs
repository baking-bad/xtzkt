namespace Xtzkt.Indexers.TezosX.Protocols.Models;

public class MetaContext
{
    public List<DelayedOperation> DelayedOps { get; init; } = [];
    public Dictionary<string, Queue<MetaContent>> QueuesByHash { get; init; } = [];
    public Dictionary<string, Queue<MetaContent>> QueuesByCracId { get; init; } = [];
}
