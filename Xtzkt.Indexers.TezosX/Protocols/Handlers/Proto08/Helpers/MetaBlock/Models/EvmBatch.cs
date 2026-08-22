using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

public class EvmBatch
{
    public int Index { get; }
    public string Hash { get; }
    public List<EvmOperation> Operations { get; }
    public string? CracId { get; }

    public EvmBatch(JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        Index = receipt.RequiredHexInt32("transactionIndex");
        Hash = tx.RequiredString("hash");
        Operations = [new EvmOperation(this, tx, receipt, trace)];

        var op = Operations[0];
        if (op.From == op.To)
        {
            var cracReceivedLog = op.Logs.FirstOrDefault(x =>
                x.RequiredString("address") == EvmRuntime.MichelsonGateway &&
                x.RequiredArray("topics")[0].RequiredString() == EvmRuntime.CracReceivedTopic);

            if (cracReceivedLog.ValueKind != JsonValueKind.Undefined)
                CracId = new AbiReader(cracReceivedLog.RequiredHexBytes("data")).ReadString(0);
        }
    }
}

public class EvmOperation : IMetaOperationContent
{
    public EvmBatch Batch { get; }
    public JsonElement Tx { get; }
    public JsonElement Receipt { get; }
    public JsonElement Trace { get; }
    public List<EvmInternalOperation> Internals { get; }
    public string From { get; }
    public string? To { get; }
    public IEnumerable<JsonElement> Logs => Trace.OptionalArray("logs")?.EnumerateArray() ?? [];

    public EvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        Batch = batch;
        Tx = tx;
        Receipt = receipt;
        Trace = trace;
        Internals = [.. EnumerateTraces(trace).Skip(1).Select(x => new EvmInternalOperation(this, x.Trace, x.Depth, x.Status, x.ParentStatus))];
        From = trace.RequiredString("from");
        To = trace.OptionalString("to");
    }

    public override string ToString()
    {
        return To != null
            ? $"{From[..7]}..{From[^4..]} -> {To[..7]}..{To[^4..]}"
            : $"{From[..7]}..{From[^4..]} -> null";
    }

    static IEnumerable<(JsonElement Trace, int Depth, OperationStatus Status, OperationStatus ParentStatus)> EnumerateTraces(JsonElement trace, int depth = 0, OperationStatus parentStatus = OperationStatus.Applied)
    {
        var status = trace.OptionalString("error") != null || trace.OptionalString("revertReason") != null
            ? OperationStatus.Failed
            : parentStatus != OperationStatus.Applied
                ? OperationStatus.Backtracked
                : OperationStatus.Applied;

        yield return (trace, depth, status, parentStatus);
        foreach (var subtrace in trace.OptionalArray("calls")?.EnumerateArray() ?? [])
            foreach (var item in EnumerateTraces(subtrace, depth + 1, status))
                yield return item;
    }
}

public class EvmInternalOperation(EvmOperation operation, JsonElement trace, int depth, OperationStatus status, OperationStatus parentStatus) : IMetaInternalOperationContent
{
    public EvmOperation Operation { get; } = operation;
    public JsonElement Trace { get; } = trace;
    public int Depth { get; } = depth;
    public OperationStatus Status { get; } = status;
    public OperationStatus ParentStatus { get; } = parentStatus;
    public string From { get; } = trace.RequiredString("from");
    public string? To { get; } = trace.OptionalString("to");
    public IEnumerable<JsonElement> Logs => Trace.OptionalArray("logs")?.EnumerateArray() ?? [];

    public override string ToString()
    {
        return To != null
            ? $"{From[..7]}..{From[^4..]} -> {To[..7]}..{To[^4..]}"
            : $"{From[..7]}..{From[^4..]} -> null";
    }
}
