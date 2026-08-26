using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto03.Helpers;

public partial class ProtoHelpers
{
    public override async Task<MetaBlock> GetMetaBlock(int level)
    {
        var t1 = GetBlueprint(level);
        var t2 = EvmRpc.GetBlockData(level);

        await Task.WhenAll(t1, t2);

        var blueprint = t1.Result;
        var (evmBlock, evmReceipts, evmTraces) = t2.Result;
        var evmReceiptsDict = evmReceipts.EnumerateArray().ToDictionary(x => x.RequiredString("transactionHash"));
        var evmTracesDict = evmTraces.EnumerateArray().ToDictionary(x => x.RequiredString("txHash"), x => x.Required("result"));

        if (evmBlock.RequiredString("parentHash") != blueprint.Predecessor)
            throw new Exception("Inconsistent evm inputs");

        if (evmBlock.RequiredHexTimestamp32("timestamp") != blueprint.Timestamp)
            throw new Exception("Inconsistent evm inputs");

        var _queuesByHash = new Dictionary<string, Queue<MetaContent>>();

        foreach (var tx in evmBlock.RequiredArray("transactions").EnumerateArray())
        {
            var hash = tx.RequiredString("hash");
            var receipt = evmReceiptsDict[hash];
            var trace = evmTracesDict[hash];
            var batch = new EvmBatch
            {
                Hash = hash,
                Index = receipt.RequiredHexInt32("transactionIndex"),
            };
            var op = GetEvmOperation(batch, tx, receipt, trace);

            var queue = new Queue<MetaContent>();
            queue.Enqueue(op);
            foreach (var internalOp in GetEvmInternalOperations(op))
                queue.Enqueue(internalOp);

            _queuesByHash.Add(batch.Hash, queue);
        }

        var batches = new List<MetaBatch>();
        var context = new MetaContext
        {
            DelayedOps = blueprint.DelayedTransactions,
            QueuesByHash = _queuesByHash,
        };

        foreach (var hash in blueprint.DelayedTransactions.Select(x => x.Hash))
        {
            if (TryReadOperation(context, hash, true) is not MetaBatch batch)
            {
                Logger.LogWarning("Operation {hash} was dropped from block {level}", hash, level);
                continue;
            }

            if (blueprint.Transactions.Any(x => x == hash))
                throw new Exception($"Operation {hash} is ambiguous, because included in both Transactions and DelayedTransactions. Cannot proceed.");

            batches.Add(batch);
        }

        foreach (var hash in blueprint.Transactions)
        {
            if (TryReadOperation(context, hash, false) is not MetaBatch batch)
            {
                Logger.LogWarning("Operation {hash} was dropped from block {level}", hash, level);
                continue;
            }

            batches.Add(batch);
        }

        if (_queuesByHash.Values.Any(x => x.Count != 0))
            throw new Exception("Not all operations were consumed");

        return new MetaBlock
        {
            Level = blueprint.Level,
            Timestamp = blueprint.Timestamp,
            Hash = evmBlock.RequiredString("hash"),
            Batches = batches,
            EvmBlock = evmBlock,
            MichelsonBlock = null,
            KernelUpgrade = blueprint.KernelUpgrade,
            KernelUpgradeTime = blueprint.KernelUpgradeTime,
        };
    }

    protected override EvmOperation GetEvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        return new EvmOperation
        {
            Batch = batch,
            Tx = tx,
            Receipt = receipt,
            Trace = trace,
            Logs = EnumerateTraces(trace, TraceLogs(trace)).First().Logs,
            From = trace.RequiredString("from"),
            To = trace.OptionalString("to"),
        };
    }

    protected override List<EvmInternalOperation> GetEvmInternalOperations(EvmOperation op)
    {
        var iops = EnumerateTraces(op.Trace, TraceLogs(op.Trace)).Skip(1).Select(x => new EvmInternalOperation
        {
            Operation = op,
            Depth = x.Depth,
            Trace = x.Trace,
            Logs = x.Logs,
            Status = x.Status,
            ParentStatus = x.ParentStatus,
            From = x.Trace.RequiredString("from"),
            To = x.Trace.OptionalString("to"),
        }).ToList();

        if (op.Receipt.RequiredArray("logs").GetArrayLength() != op.Logs.Count + iops.Sum(x => x.Logs.Count))
            throw new Exception("Logs in traces != logs in receipt");

        return iops;
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
