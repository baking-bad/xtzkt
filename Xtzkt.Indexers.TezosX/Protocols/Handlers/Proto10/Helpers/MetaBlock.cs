using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Blake2Fast;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10.Helpers;

public partial class ProtoHelpers
{
    public override async Task<MetaBlock> GetMetaBlock(int level, Task<JsonElement> rawBlueprintTask)
    {
        var t1 = GetBlueprint(level, rawBlueprintTask);
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

        var michelsonBatches = michelsonBlock?
            .RequiredArray("operations", 4)[3]
            .EnumerateArray()
            .ToList()
            ?? [];

        HashSet<string>? syntheticHashes = null;

        var evmOps = new Dictionary<string, EvmOpContext>();
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

            var isSynthetic = op.From == op.To &&
                (syntheticHashes ??= [.. michelsonBatches.Select(x => GetEvmSyntheticHash(x.RequiredString("hash")))]).Contains(hash);

            evmOps.Add(hash, new EvmOpContext
            {
                Operation = op,
                RootFrame = GetEvmRootFrame(op, GetEvmInternalOperations(op, isSynthetic)),
            });
        }

        var michelsonOpsList = new List<MichelsonOpContext>(michelsonBatches.Count);
        foreach (var opg in michelsonBatches)
        {
            var batch = new MichelsonBatch { Hash = opg.RequiredString("hash") };

            michelsonOpsList.Add(new MichelsonOpContext
            {
                Batch = batch,
                Contents = [.. GetMichelsonOperations(batch, opg).Select(op => new MichelsonContent
                {
                    Op = op,
                    Internals = GetMichelsonInternalContents(GetMichelsonInternalOperations(op)),
                })],
            });
        }

        var michelsonOps = PairOpContexts(evmOps, michelsonOpsList);

        var context = new MetaBlockContext
        {
            DelayedOps = blueprint.DelayedTransactions,
            EvmOps = evmOps,
            MichelsonOps = michelsonOps,
        };

        if (michelsonBlock != null)
        {
            foreach (var op in context.EvmOps.Values)
                MatchEvmOp(op);

            foreach (var op in context.MichelsonOps.Values)
                MatchMichelsonOp(op);
        }

        var batches = new List<MetaBatch>();
        foreach (var hash in blueprint.DelayedTransactions.Select(x => x.Hash))
        {
            if (ReadOperation(context, hash, true) is not MetaBatch batch)
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
            if (ReadOperation(context, hash, false) is not MetaBatch batch)
            {
                Logger.LogWarning("Operation {hash} was dropped from block {level}", hash, level);
                continue;
            }

            batches.Add(batch);
        }

        if (context.EvmOps.Count != 0 || context.MichelsonOps.Count != 0)
            throw new Exception("Not all operations were consumed");

        return new MetaBlock
        {
            Level = blueprint.Level,
            Timestamp = blueprint.Timestamp,
            Hash = evmBlock.RequiredHexBytes("hash"),
            Batches = batches,
            EvmBlock = evmBlock,
            MichelsonBlock = michelsonBlock,
            //KernelUpgrade = blueprint.KernelUpgrade,
            //KernelUpgradeTime = blueprint.KernelUpgradeTime,
        };
    }

    protected bool IsEvmCrac(EvmOperation evmOp, [NotNullWhen(true)] out string? cracId)
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

    protected override EvmOperation GetEvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt, JsonElement trace)
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

    protected override List<EvmInternalOperation> GetEvmInternalOperations(EvmOperation op)
    {
        return GetEvmInternalOperations(op, IsEvmCrac(op, out _));
    }

    List<EvmInternalOperation> GetEvmInternalOperations(EvmOperation op, bool isEvmCrac)
    {
        return [.. EnumerateTraces(op.Trace, isEvmCrac).Skip(1).Select(x => new EvmInternalOperation
        {
            Operation = op,
            Depth = x.Depth,
            Trace = x.Trace,
            Logs = x.Trace.OptionalArray("logs")?.EnumerateArray().ToList() ?? [],
            Status = x.Status,
            ParentStatus = x.ParentStatus,
            StaticRootStatus = x.StaticRootStatus,
            From = x.Trace.RequiredString("from"),
            To = x.Trace.OptionalString("to"),
        })];
    }

    protected IEnumerable<TraceFrame> EnumerateTraces(
        JsonElement trace, bool isEvmCrac, int depth = 0, OperationStatus parentStatus = OperationStatus.Applied, OperationStatus? staticRootStatus = null)
    {
        var status = trace.TraceStatus(parentStatus);

        if (staticRootStatus == null && depth > 0 && trace.IsStaticCall())
            staticRootStatus = parentStatus;

        yield return new TraceFrame
        {
            Trace = trace,
            Depth = depth,
            Status = status,
            ParentStatus = parentStatus,
            StaticRootStatus = staticRootStatus,
        };

        if (trace.OptionalArray("calls") is not JsonElement subtraces)
            yield break;

        // calls right below the root of an evm crac or below a crac gateway call come from the michelson side
        var isCracFrameRoot = (depth == 0 && isEvmCrac) || EvmRuntime.IsCracCall(trace.OptionalString("to"), trace);

        foreach (var subtrace in subtraces.EnumerateArray())
        {
            if (isCracFrameRoot)
            {
                // skip incoming cross-runtime static trees as they have no gateway call to be matched with
                if (subtrace.IsStaticCall())
                {
                    #region debug
                    if (HasLogs(subtrace))
                        throw new Exception("Unexpected logs in a static call");
                    #endregion
                    continue;
                }

                // skip alias materializations
                if (subtrace.RequiredString("from") == EvmRuntime.TezosXCaller && !HasCalls(subtrace))
                    continue;
            }

            foreach (var item in EnumerateTraces(subtrace, false, depth + 1, status, staticRootStatus))
                yield return item;
        }
    }

    static bool HasCalls(JsonElement trace)
    {
        return trace.OptionalArray("calls") is JsonElement calls && calls.GetArrayLength() != 0;
    }

    static bool HasLogs(JsonElement trace)
    {
        if (trace.OptionalArray("logs") is JsonElement logs && logs.GetArrayLength() != 0)
            return true;

        foreach (var subtrace in trace.OptionalArray("calls")?.EnumerateArray() ?? [])
            if (HasLogs(subtrace))
                return true;

        return false;
    }

    protected readonly struct TraceFrame
    {
        public required JsonElement Trace { get; init; }
        public required int Depth { get; init; }
        public required OperationStatus Status { get; init; }
        public required OperationStatus ParentStatus { get; init; }
        public required OperationStatus? StaticRootStatus { get; init; }
    }

    static EvmRootFrame GetEvmRootFrame(EvmOperation op, List<EvmInternalOperation> internals)
    {
        var root = new EvmRootFrame { Op = op };
        var path = new List<EvmFrame> { root };
        foreach (var iop in internals)
        {
            if (iop.Depth < 1 || iop.Depth > path.Count)
                throw new Exception("Invalid call trace");

            path.RemoveRange(iop.Depth, path.Count - iop.Depth);
            var frame = new EvmInternalFrame { Op = iop };
            path[^1].Calls.Add(frame);
            path.Add(frame);
        }
        return root;
    }

    const string CracBeginTag = "cross_runtime_call";
    const string CracEndTag = "cross_runtime_call_end";

    List<MichelsonInternalContent> GetMichelsonInternalContents(List<MichelsonInternalOperation> iops)
    {
        var pos = 0;
        var items = GetMichelsonInternalContents(iops, ref pos);
        if (pos != iops.Count)
            throw new Exception("Unexpected crac event");
        return items;
    }

    List<MichelsonInternalContent> GetMichelsonInternalContents(List<MichelsonInternalOperation> iops, ref int pos)
    {
        var items = new List<MichelsonInternalContent>();
        while (pos < iops.Count)
        {
            var iop = iops[pos];
            switch (GetCracMarker(iop))
            {
                case CracBeginTag:
                    items.Add(GetMichelsonCracContent(iops, ref pos));
                    break;
                case CracEndTag:
                    return items;
                default:
                    items.Add(new MichelsonInternalOpContent { Op = iop });
                    pos++;
                    break;
            }
        }
        return items;
    }

    MichelsonCracFrameContent GetMichelsonCracContent(List<MichelsonInternalOperation> iops, ref int pos)
    {
        // skip crac begin event
        var begin = iops[pos++];

        // skip alias originations
        while (pos < iops.Count &&
            iops[pos].Content.RequiredString("kind") == "origination" &&
            iops[pos].From == MichelsonRuntime.NullAddress)
            pos++;

        if (pos == iops.Count ||
            iops[pos].Content.RequiredString("kind") != "transaction" ||
            iops[pos].To == null)
            throw new Exception("Crac frame target call missed");

        // take target call
        var targetCall = iops[pos++];

        // take internal ops
        var items = GetMichelsonInternalContents(iops, ref pos);

        // skip crac end event
        if (pos == iops.Count || GetCracMarker(iops[pos++]) != CracEndTag)
            throw new Exception("Incomplete crac frame");

        return new MichelsonCracFrameContent
        {
            Begin = begin,
            TargetCall = targetCall,
            Internals = items,
            HasFailure = GetStatus(targetCall) == OperationStatus.Failed ||
                items.Any(x => x is MichelsonInternalOpContent { Op: var op } && GetStatus(op) == OperationStatus.Failed),
        };
    }

    string? GetCracMarker(MichelsonInternalOperation iop)
    {
        if (iop.From == MichelsonRuntime.CracOrigin && iop.Content.RequiredString("kind") == "event")
        {
            var tag = iop.Content.OptionalString("tag");
            if (tag is CracBeginTag or CracEndTag)
                return tag;
        }
        return null;
    }

    static string GetCracId(MichelsonInternalOperation marker)
    {
        return marker.Content.Required("payload").RequiredString("string");
    }

    Dictionary<string, MichelsonOpContext> PairOpContexts(Dictionary<string, EvmOpContext> evmOps, List<MichelsonOpContext> michelsonOpsList)
    {
        var michelsonOps = michelsonOpsList.ToDictionary(x => x.Batch.Hash);

        var anyFromNullAddress = michelsonOpsList.Any(x => x.Contents.Any(y => y.Op.From == MichelsonRuntime.NullAddress));
        foreach (var evmOp in evmOps.Values)
        {
            if (!anyFromNullAddress ||
                !michelsonOps.Remove(GetMichelsonSyntheticHash(evmOp.Operation.Batch.Hash), out var synthetic))
                continue;

            if (synthetic.Contents is not [var content] ||
                content.Op.From != MichelsonRuntime.NullAddress ||
                content.Internals.Any(x => x is not MichelsonCracFrameContent))
                throw new Exception($"Invalid synthetic operation {synthetic.Batch.Hash}");

            #region debug
            var cracId = $"{EvmRuntime.RuntimeId}-{evmOp.Operation.Batch.Index}";
            if (content.Internals.Cast<MichelsonCracFrameContent>().Any(x => GetCracId(x.Begin) != cracId))
                throw new Exception($"Unexpected crac id in synthetic operation {synthetic.Batch.Hash}");
            #endregion

            evmOp.MichelsonContents = [.. content.Internals.Cast<MichelsonCracFrameContent>()];
        }

        var nativeIndex = 0;
        var anySelfAddressed = evmOps.Values.Any(x => x.Operation.From == x.Operation.To);
        foreach (var michelsonOp in michelsonOpsList)
        {
            if (!michelsonOps.ContainsKey(michelsonOp.Batch.Hash))
                continue;

            var index = nativeIndex++;

            if (!anySelfAddressed ||
                !evmOps.Remove(GetEvmSyntheticHash(michelsonOp.Batch.Hash), out var synthetic))
                continue;

            if (synthetic.Operation.From != synthetic.Operation.To)
                throw new Exception($"Invalid synthetic transaction {synthetic.Operation.Batch.Hash}");

            #region debug
            if (IsEvmCrac(synthetic.Operation, out var cracId) && cracId != $"{MichelsonRuntime.RuntimeId}-{index}")
                throw new Exception($"Unexpected crac id in synthetic transaction {synthetic.Operation.Batch.Hash}");
            #endregion

            michelsonOp.EvmRoot = synthetic.RootFrame;
        }

        foreach (var michelsonOp in michelsonOps.Values)
            if (michelsonOp.Contents is [{ Internals: [MichelsonCracFrameContent, ..] } content] && content.Op.From == MichelsonRuntime.NullAddress)
                throw new Exception($"Synthetic operation {michelsonOp.Batch.Hash} has no pair");

        foreach (var evmOp in evmOps.Values)
            if (IsEvmCrac(evmOp.Operation, out _))
                throw new Exception($"Synthetic operation {evmOp.Operation.Batch.Hash} has no pair");

        return michelsonOps;
    }

    static string GetMichelsonSyntheticHash(string evmTransactionHash)
    {
        return Hashes.FormatMichelsonOperationHash(Blake2b.ComputeHash(32, [.. "michelson"u8, .. Hex.GetBytes(evmTransactionHash)]));
    }

    static string GetEvmSyntheticHash(string michelsonOperationHash)
    {
        return Keccak256.GetHash([.. "evm"u8, .. Hashes.ParseMichelsonOperationHash(michelsonOperationHash)]);
    }

    protected static List<MichelsonOperation> GetMichelsonOperations(MichelsonBatch batch, JsonElement opg)
    {
        return [.. opg.RequiredArray("contents").EnumerateArray() .Select(x => new MichelsonOperation
        {
            Batch = batch,
            Content = x,
            From = x.RequiredString("source"),
            To = x.OptionalString("destination"),
        })];
    }

    protected static List<MichelsonInternalOperation> GetMichelsonInternalOperations(MichelsonOperation op)
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
