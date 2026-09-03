using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols.Models;

public sealed class MichelsonBatch
{
    public int Index { get; init; }
    public required string Hash { get; init; }
}

public sealed class MichelsonOperation : MetaOperation
{
    public required MichelsonBatch Batch { get; init; }
    public JsonElement Content { get; init; }
    public required string From { get; init; }
    public string? To { get; init; }
}

public sealed class MichelsonInternalOperation : MetaInternalOperation
{
    public required MichelsonOperation Operation { get; init; }
    public JsonElement Content { get; init; }
    public required string From { get; init; }
    public string? To { get; init; }
}

public sealed class MichelsonDeposit : MetaOperation
{
    public required DelayedOperation Deposit { get; init; }
    public required MichelsonOperation FeederCall { get; init; }
    public List<MichelsonInternalOperation> BridgeCalls { get; init; } = [];
}
