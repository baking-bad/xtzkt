using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Models;

public sealed class EvmBatch
{
    public int Index { get; init; }
    public required string Hash { get; init; }
}

public sealed class EvmOperation : MetaOperation
{
    public required EvmBatch Batch { get; init; }
    public required JsonElement Tx { get; init; }
    public required JsonElement Receipt { get; init; }
    public required JsonElement Trace { get; init; }
    public required List<JsonElement> Logs { get; init; }
    public required string From { get; init; }
    public string? To { get; init; }

    // needed for bug-ish kernels, adding intrinsic gas to every frame
    public int FrameGasOffset { get; init; }
}

public sealed class EvmInternalOperation : MetaInternalOperation
{
    public required EvmOperation Operation { get; init; }
    public required int Depth { get; init; }
    public required JsonElement Trace { get; init; }
    public required List<JsonElement> Logs { get; init; }
    public required OperationStatus Status { get; init; }
    public required OperationStatus ParentStatus { get; init; }
    public required string From { get; init; }
    public string? To { get; init; }

    // needed for bug-ish kernels, adding intrinsic gas to every frame
    public int FrameGasOffset { get; init; }
}

public sealed class EvmDeposit : MetaOperation
{
    public required DelayedOperation Deposit { get; init; }
    public required EvmOperation FeederCall { get; init; }
    public List<EvmInternalOperation> BridgeCalls { get; init; } = [];
}
