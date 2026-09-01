using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols.Models;

public sealed class MetaBlock
{
    public required int Level { get; init; }
    public required DateTime Timestamp { get; init; }
    public required byte[] Hash { get; init; }
    public required List<MetaBatch> Batches { get; init; }

    public JsonElement EvmBlock { get; init; }
    public JsonElement? MichelsonBlock { get; init; }

    public string? KernelUpgrade { get; init; }
    public DateTime? KernelUpgradeTime { get; init; }
}

public class MetaBatch
{
    public bool Delayed { get; init; }
    public required byte[] Hash { get; init; }
    public List<MetaOperation> Operations { get; init; } = [];
}

public abstract class MetaOperation : MetaContent
{
    public List<MetaInternalOperation> Internals { get; init; } = [];
}

public abstract class MetaInternalOperation : MetaContent
{
    public MetaContent? CracParent { get; set; }
}

public abstract class MetaContent { }
