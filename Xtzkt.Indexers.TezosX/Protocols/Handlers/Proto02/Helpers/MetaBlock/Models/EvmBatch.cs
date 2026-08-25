using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers.MetaBlock;

public class EvmBatch
{
    public int Index { get; }
    public string Hash { get; }
    public List<EvmOperation> Operations { get; }

    public EvmBatch(JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        Index = receipt.RequiredHexInt32("transactionIndex");
        Hash = tx.RequiredString("hash");
        Operations = [new EvmOperation(this, tx, receipt, trace)];

        if (receipt.RequiredArray("logs").GetArrayLength() !=
            Operations[0].Logs.Count + Operations[0].Internals.Sum(x => x.Logs.Count))
            throw new Exception("Logs in traces != logs in receipt");
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
    public List<JsonElement> Logs { get; }

    public EvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        Batch = batch;
        Tx = tx;
        Receipt = receipt;
        Trace = trace;
        From = trace.RequiredString("from");
        To = trace.OptionalString("to");

        // the root frame carries the logs of the entire transaction
        var traces = EnumerateTraces(trace, TraceLogs(trace)).ToList();

        Logs = traces[0].Logs;
        Internals = [.. traces.Skip(1).Select(x => new EvmInternalOperation(this, x.Trace, x.Depth, x.Status, x.ParentStatus, x.Logs))];
    }

    public override string ToString()
    {
        return To != null
            ? $"{From[..7]}..{From[^4..]} -> {To[..7]}..{To[^4..]}"
            : $"{From[..7]}..{From[^4..]} -> null";
    }

    static List<JsonElement> TraceLogs(JsonElement trace)
    {
        return trace.OptionalArray("logs") is JsonElement logs ? [.. logs.EnumerateArray()] : [];
    }

    static IEnumerable<(JsonElement Trace, int Depth, OperationStatus Status, OperationStatus ParentStatus, List<JsonElement> Logs)> EnumerateTraces(
        JsonElement trace, List<JsonElement> subtreeLogs, string? context = null, int depth = 0, OperationStatus parentStatus = OperationStatus.Applied)
    {
        var status = HasFailed(trace)
            ? OperationStatus.Failed
            : parentStatus != OperationStatus.Applied
                ? OperationStatus.Backtracked
                : OperationStatus.Applied;

        context = ExecutionContext(trace, context);

        var subtraces = trace.OptionalArray("calls")?.EnumerateArray().ToList() ?? [];
        var subtraceLogs = new List<List<JsonElement>>(subtraces.Count);
        var ownLogs = new List<JsonElement>();

        var pos = 0;
        foreach (var subtrace in subtraces)
        {
            // a subtrace's log count is a snapshot of the parent frame's log list taken right after the subcall
            // ended, so it's an exact end boundary of the subtrace's contribution; the start boundary is not
            // recorded anywhere, so it's recovered by walking back while the log could have been emitted inside
            // the subtrace (a reverted subtrace contributes nothing, so there's nothing to walk back for)
            var end = Math.Clamp(subtrace.OptionalArray("logs")?.GetArrayLength() ?? 0, pos, subtreeLogs.Count);
            var start = end;

            if (start > pos && !HasFailed(subtrace))
            {
                var subtreeContexts = SubtreeContexts(subtrace, context);
                while (start > pos && subtreeLogs[start - 1].RequiredString("address") is string address && (subtreeContexts.Contains(address) || address != context))
                    start--;
            }

            ownLogs.AddRange(subtreeLogs[pos..start]);
            subtraceLogs.Add(subtreeLogs[start..end]);
            pos = end;
        }

        ownLogs.AddRange(subtreeLogs[pos..]);

        yield return (trace, depth, status, parentStatus, ownLogs);

        for (int i = 0; i < subtraces.Count; i++)
            foreach (var item in EnumerateTraces(subtraces[i], subtraceLogs[i], context, depth + 1, status))
                yield return item;
    }

    static bool HasFailed(JsonElement trace)
    {
        return trace.OptionalString("error") != null || trace.OptionalString("revertReason") != null;
    }

    static string? ExecutionContext(JsonElement trace, string? callerContext)
    {
        return trace.RequiredString("type") is "DELEGATECALL" or "CALLCODE"
            ? callerContext
            : trace.OptionalString("to");
    }

    static HashSet<string> SubtreeContexts(JsonElement trace, string? callerContext)
    {
        var contexts = new HashSet<string>();
        Collect(trace, callerContext);
        return contexts;

        void Collect(JsonElement trace, string? callerContext)
        {
            var context = ExecutionContext(trace, callerContext);
            if (context != null)
                contexts.Add(context);

            foreach (var subtrace in trace.OptionalArray("calls")?.EnumerateArray() ?? [])
                Collect(subtrace, context);
        }
    }
}

public class EvmInternalOperation(EvmOperation operation, JsonElement trace, int depth, OperationStatus status, OperationStatus parentStatus, List<JsonElement> logs) : IMetaInternalOperationContent
{
    public EvmOperation Operation { get; } = operation;
    public JsonElement Trace { get; } = trace;
    public int Depth { get; } = depth;
    public OperationStatus Status { get; } = status;
    public OperationStatus ParentStatus { get; } = parentStatus;
    public string From { get; } = trace.RequiredString("from");
    public string? To { get; } = trace.OptionalString("to");
    public List<JsonElement> Logs { get; } = logs;

    public override string ToString()
    {
        return To != null
            ? $"{From[..7]}..{From[^4..]} -> {To[..7]}..{To[^4..]}"
            : $"{From[..7]}..{From[^4..]} -> null";
    }
}
