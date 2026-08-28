using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto06.Helpers;

class ProtoHelpers(ProtocolHandler protocol) : Proto05.Helpers.ProtoHelpers(protocol)
{
    protected override EvmOperation GetEvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        var from = trace.RequiredString("from");
        return new EvmOperation
        {
            Batch = batch,
            Tx = tx,
            Receipt = receipt,
            Trace = trace,
            Logs = BuildFrames(trace).Logs,
            From = from,
            To = trace.OptionalString("to"),
            // deposits are excluded, because kernel synthesizes their traces by hand, with the plain gas used
            FrameGasOffset = from != EvmRuntime.NullAddress ? EvmGas.GetIntrinsicGas(tx) : 0,
        };
    }

    protected override List<EvmInternalOperation> GetEvmInternalOperations(EvmOperation op)
    {
        var iops = new List<EvmInternalOperation>();
        foreach (var subframe in BuildFrames(op.Trace).Subframes)
            AddInternalOperations(op, subframe, iops);

        if (op.Receipt.RequiredArray("logs").GetArrayLength() != op.Logs.Count + iops.Sum(x => x.Logs.Count))
            throw new Exception("Logs in traces != logs in receipt");

        return iops;
    }

    static void AddInternalOperations(EvmOperation op, Frame frame, List<EvmInternalOperation> dest)
    {
        dest.Add(new EvmInternalOperation
        {
            Operation = op,
            Depth = frame.Depth,
            Trace = frame.Trace,
            Logs = frame.Logs,
            Status = frame.Status,
            ParentStatus = frame.ParentStatus,
            From = frame.Trace.RequiredString("from"),
            To = frame.Trace.OptionalString("to"),
            FrameGasOffset = op.FrameGasOffset,
        });

        foreach (var subframe in frame.Subframes)
            AddInternalOperations(op, subframe, dest);
    }

    static Frame BuildFrames(JsonElement trace)
    {
        var root = BuildFrame(trace, null, 0, OperationStatus.Applied, null);

        if (root.Subframes.Count == 0)
        {
            // skip assigning by address, because of synthetic deposits, emitting logs from null address
            root.Logs.AddRange(root.Trace.OptionalArray("logs")?.EnumerateArray() ?? []);
        }
        else
        {
            AssignLogs(root);
        }

        return root;
    }

    static Frame BuildFrame(JsonElement trace, Frame? parent, int depth, OperationStatus parentStatus, string? callerContext)
    {
        var failed = trace.OptionalString("error") != null || trace.OptionalString("revertReason") != null;
        var frame = new Frame
        {
            Trace = trace,
            Depth = depth,
            Status = failed
                ? OperationStatus.Failed
                : parentStatus != OperationStatus.Applied
                    ? OperationStatus.Backtracked
                    : OperationStatus.Applied,
            ParentStatus = parentStatus,
            Parent = parent,
            Context = trace.RequiredString("type") is "DELEGATECALL" or "CALLCODE"
                ? callerContext
                : trace.OptionalString("to"),
        };

        foreach (var subtrace in trace.OptionalArray("calls")?.EnumerateArray() ?? [])
            frame.Subframes.Add(BuildFrame(subtrace, frame, depth + 1, frame.Status, frame.Context));

        return frame;
    }

    static void AssignLogs(Frame frame)
    {
        foreach (var subframe in frame.Subframes)
            AssignLogs(subframe);

        foreach (var log in frame.Trace.OptionalArray("logs")?.EnumerateArray() ?? [])
        {
            var address = log.RequiredString("address");

            var emitter = frame;
            while (emitter != null && emitter.Context != address)
                emitter = emitter.Parent;

            // 1. In case emitter is not found, the indexer should stop (and will stop because of assertion check above).
            // 2. A log drained into a failed frame may actually belong to an applied ancestor with
            // the same context (e.g. B -> A -> B -> A(failed) with logs [B, A] landing on the failed A).
            // The trace carries no emission info to tell such cases apart, so exact assignment is
            // fundamentally impossible here; we drop the log and rely on the assertion above to stop
            // the indexer if it ever matters. Fixed only by the Tezos X kernel's exact per-frame logs.

            if (emitter?.Status == OperationStatus.Applied)
                emitter.Logs.Add(log);
        }
    }

    class Frame
    {
        public required JsonElement Trace { get; init; }
        public required int Depth { get; init; }
        public required OperationStatus Status { get; init; }
        public required OperationStatus ParentStatus { get; init; }
        public required Frame? Parent { get; init; }
        public required string? Context { get; init; }
        public List<Frame> Subframes { get; } = [];
        public List<JsonElement> Logs { get; } = [];
    }
}
