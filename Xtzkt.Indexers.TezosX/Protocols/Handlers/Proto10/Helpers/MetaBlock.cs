using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10.Helpers;

public partial class ProtoHelpers
{
    public virtual async Task<MetaBlock> GetMetaBlock(int level)
    {
        var t1 = GetBlueprint(level);
        var t2 = EvmRpc.GetBlockData(level);
        var t3 = level >= Cache.Chain.Get().MichelsonActivationLevel
            ? MichelsonRpc.GetBlockAsync(level)
            : Task.FromResult<JsonElement>(default);

        await Task.WhenAll(t1, t2, t3);

        var blueprint = t1.Result;
        var (evmBlock, evmReceipts, evmTraces) = t2.Result;
        var evmReceiptsDict = evmReceipts.EnumerateArray().ToDictionary(x => x.RequiredString("transactionHash"));
        var evmTracesDict = evmTraces.EnumerateArray().ToDictionary(x => x.RequiredString("txHash"), x => x.Required("result"));
        var michelsonBlock = t3.Result.ValueKind != JsonValueKind.Undefined ? t3.Result : (JsonElement?)null;

        if (evmBlock.RequiredString("parentHash") != blueprint.Predecessor)
            throw new Exception("Inconsistent evm inputs");

        if (evmBlock.RequiredHexTimestamp32("timestamp") != blueprint.Timestamp)
            throw new Exception("Inconsistent evm inputs");

        if (michelsonBlock != null && michelsonBlock.Value.Required("header").RequiredDateTime("timestamp") != blueprint.Timestamp)
            throw new Exception("Inconsistent michlson inputs");

        var _queuesByHash = new Dictionary<string, Queue<MetaContent>>();
        var _queuesByCracId = new Dictionary<string, Queue<MetaContent>>();

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

            if (IsEvmCrac(op, out var cracId))
                _queuesByCracId.Add(cracId, queue);
            else
                _queuesByHash.Add(batch.Hash, queue);
        }

        var michelsonBatches = michelsonBlock?
            .RequiredArray("operations", 4)[3]
            .EnumerateArray()
            .ToList()
            ?? [];

        var index = 0;
        foreach (var opg in michelsonBatches)
        {
            var batch = new MichelsonBatch
            {
                Index = index++,
                Hash = opg.RequiredString("hash"),
            };
            var ops = GetMichelsonOperations(batch, opg);

            var queue = new Queue<MetaContent>();

            queue.Enqueue(ops[0]);
            var iops = GetMichelsonInternalOperations(ops[0]);
            foreach (var internalOp in iops)
                queue.Enqueue(internalOp);

            foreach (var op in ops.Skip(1))
            {
                queue.Enqueue(op);
                foreach (var internalOp in GetMichelsonInternalOperations(op))
                    queue.Enqueue(internalOp);
            }

            if (IsMichelsonCrac(ops[0], iops, out var cracId))
                _queuesByCracId.Add(cracId, queue);
            else
                _queuesByHash.Add(batch.Hash, queue);
        }

        var batches = new List<MetaBatch>();
        var context = new MetaContext
        {
            DelayedOps = blueprint.DelayedTransactions,
            QueuesByHash = _queuesByHash,
            QueuesByCracId = _queuesByCracId,
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

        if (_queuesByCracId.Values.Any(x => x.Count != 0))
            throw new Exception("Not all crac internals were consumed");

        return new MetaBlock
        {
            Level = blueprint.Level,
            Timestamp = blueprint.Timestamp,
            Hash = evmBlock.RequiredString("hash"),
            Batches = batches,
            EvmBlock = evmBlock,
            MichelsonBlock = michelsonBlock,
            KernelUpgrade = blueprint.KernelUpgrade,
            KernelUpgradeTime = blueprint.KernelUpgradeTime,
        };
    }

    protected virtual bool IsEvmCrac(EvmOperation evmOp, [NotNullWhen(true)] out string? cracId)
    {
        if (evmOp.From == evmOp.To)
        {
            var cracReceivedLog = evmOp.Logs.FirstOrDefault(x =>
                x.RequiredString("address") == EvmRuntime.MichelsonGateway &&
                x.RequiredArray("topics")[0].RequiredString() == EvmRuntime.CracReceivedTopic);

            if (cracReceivedLog.ValueKind != JsonValueKind.Undefined)
            {
                cracId = new AbiReader(cracReceivedLog.RequiredHexBytes("data")).ReadString(0);
                return true;
            }
        }
        cracId = null;
        return false;
    }

    protected virtual EvmOperation GetEvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        return new EvmOperation
        {
            Batch = batch,
            Tx = tx,
            Receipt = receipt,
            Trace = trace,
            Logs = trace.OptionalArray("logs")?.EnumerateArray().ToList() ?? [],
            From = trace.RequiredString("from"),
            To = trace.OptionalString("to"),
        };
    }

    protected virtual List<EvmInternalOperation> GetEvmInternalOperations(EvmOperation op)
    {
        return [.. EnumerateTraces(op.Trace).Skip(1).Select(x => new EvmInternalOperation
        {
            Operation = op,
            Depth = x.Depth,
            Trace = x.Trace,
            Logs = x.Trace.OptionalArray("logs")?.EnumerateArray().ToList() ?? [],
            Status = x.Status,
            ParentStatus = x.ParentStatus,
            From = x.Trace.RequiredString("from"),
            To = x.Trace.OptionalString("to"),
        })];
    }

    protected virtual IEnumerable<(JsonElement Trace, int Depth, OperationStatus Status, OperationStatus ParentStatus)> EnumerateTraces(
        JsonElement trace, int depth = 0, OperationStatus parentStatus = OperationStatus.Applied)
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

    protected virtual bool IsMichelsonCrac(MichelsonOperation op, List<MichelsonInternalOperation> iops, [NotNullWhen(true)] out string? cracId)
    {
        if (iops.Count != 0 && op.From == MichelsonRuntime.NullAddress)
        {
            var iop = iops[0];
            if (iop.From == MichelsonRuntime.CracOrigin &&
                iop.Content.RequiredString("kind") == "event" &&
                iop.Content.RequiredString("tag") == "cross_runtime_call")
            {
                cracId = iop.Content.Required("payload").RequiredString("string");
                return true;
            }
        }
        cracId = null;
        return false;
    }

    protected virtual List<MichelsonOperation> GetMichelsonOperations(MichelsonBatch batch, JsonElement opg)
    {
        return [.. opg.RequiredArray("contents").EnumerateArray() .Select(x => new MichelsonOperation
        {
            Batch = batch,
            Content = x,
            From = x.RequiredString("source"),
            To = x.OptionalString("destination"),
        })];
    }

    protected virtual List<MichelsonInternalOperation> GetMichelsonInternalOperations(MichelsonOperation op)
    {
        return [.. op.Content.Required("metadata")
            .OptionalArray("internal_operation_results")?
            .EnumerateArray()
            .Select(x => new MichelsonInternalOperation
            {
                Operation = op,
                Content = x,
                From = x.RequiredString("source"),
                To = x.OptionalString("destination"),
            })
            ?? []];
    }
}
