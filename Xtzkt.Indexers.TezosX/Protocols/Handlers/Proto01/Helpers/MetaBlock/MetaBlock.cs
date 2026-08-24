using System.Text;
using System.Text.Json;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

public class MetaBlock : IMetaBlock
{
    public required int Level { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Hash { get; init; }
    public required List<IMetaBatch> Batches { get; init; }
    public required List<IDelayedTransaction> Delayed { get; init; }

    public JsonElement EvmBlock { get; init; }
    public JsonElement? MichelsonBlock { get; init; }

    public string? KernelUpgrade { get; init; }
    public DateTime? KernelUpgradeTime { get; init; }

    public override string ToString()
    {
        return string.Join("\n", Batches.Select(x => x.ToString()));
    }
}

public class MetaBatch(string hash, bool delayed) : IMetaBatch
{
    public string Hash { get; } = hash;
    public bool Delayed { get; } = delayed;
    public List<IMetaOperation> Operations { get; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Hash);
        foreach (var op in Operations)
        {
            sb.AppendLine($"{op.Content.ToString()}");
            foreach (var iop in op.Internals)
                sb.AppendLine($"    {iop.Content.ToString()}");
        }
        return sb.ToString();
    }
}

public class MetaOperation(IMetaOperationContent content) : IMetaOperation
{
    public IMetaOperationContent Content { get; } = content;
    public List<IMetaInternalOperation> Internals { get; } = [];
}

public class MetaInternalOperation(IMetaInternalOperationContent content, IMetaContent? cracParent = null) : IMetaInternalOperation
{
    public IMetaInternalOperationContent Content { get; } = content;

    public IMetaContent? CracParent { get; } = cracParent;
}