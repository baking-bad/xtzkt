using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Cache;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers;

class ProtoHelpers(ProtocolHandler protocol) : Proto01.Helpers.ProtoHelpers(protocol)
{
    #region addresses
    public override async Task<XEvmAddress> GetOrCreateXEvmAddress(string hash)
    {
        if (await Cache.Addresses.GetOrDefaultAsync(hash) is XEvmAddress address)
            return address;

        return await CreateXEvmUser(hash);
    }

    public override async Task<XEvmContract> GetOrCreateXEvmContract(string hash)
    {
        // only makes sense in the genesis era, where contracts are unobservable without call traces
        throw new NotImplementedException();
    }
    #endregion

    #region blueprint
    protected override DelayedOperation ParseDelayedOperation(DelayedTransaction cached)
    {
        return cached.Kind switch
        {
            "deposit" => ParseDelayedXtzDeposit(cached.Hash, cached.Payload),
            "fa_deposit" => ParseDelayedFaDeposit(cached.Hash, cached.Payload),
            "transaction" => ParseDelayedEvmTransaction(cached.Hash),
            _ => throw new FormatException("Invalid delayed transactions format"),
        };
    }

    protected override DelayedXtzDeposit ParseDelayedXtzDeposit(byte[] hash, byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        if (stream.Read() is not RlpList rlp || stream.CanRead)
            throw new FormatException("Invalid delayed xtz deposit rlp");

        // TODO: from Farfadet 6.0 the receiver may be an RlpList `[1, 22-byte tezos contract]` (DepositReceiver::Tezos) — this will throw on it
        if (rlp is [RlpItem e0, RlpItem e1, RlpItem e2, RlpItem e3])
        {
            return new DelayedXtzDeposit
            {
                Hash = Hex.GetString(hash),
                Amount = new BigInteger(e0.Data, true, true),
                Receiver = Hex.GetString(e1.Data),
                InboxLevel = HexNumber.GetInt32(e2.Data),
                InboxMessageId = HexNumber.GetInt32(e3.Data),
            };
        }
        throw new FormatException("Invalid delayed xtz deposit rlp");
    }

    protected static DelayedFaDeposit ParseDelayedFaDeposit(byte[] hash, byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        if (stream.Read() is not RlpList rlp || stream.CanRead)
            throw new FormatException("Invalid delayed fa deposit rlp");

        if (rlp is [RlpItem e0, RlpItem e1, RlpList e2, RlpItem e3, RlpItem e4, RlpItem e5])
        {
            return new DelayedFaDeposit
            {
                Hash = Hex.GetString(hash),
                Amount = new BigInteger(e0.Data, true, true),
                Receiver = Hex.GetString(e1.Data),
                Proxy = e2 is [RlpItem _e2]
                    ? Hex.GetString(_e2.Data)
                    : e2 is []
                        ? null
                        : throw new FormatException("Invalid delayed fa deposit rlp"),
                TicketHash = e3.Data,
                InboxLevel = HexNumber.GetInt32(e4.Data),
                InboxMessageId = HexNumber.GetInt32(e5.Data),
            };
        }
        throw new FormatException("Invalid delayed fa deposit rlp");
    }
    #endregion

    #region meta block
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
            StaticRootStatus = x.StaticRootStatus,
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

    static IEnumerable<TraceFrame> EnumerateTraces(
        JsonElement trace, List<JsonElement> subtreeLogs, string? context = null, int depth = 0, OperationStatus parentStatus = OperationStatus.Applied, OperationStatus? staticRootStatus = null)
    {
        var status = trace.TraceStatus(parentStatus);

        if (staticRootStatus == null && depth > 0 && trace.IsStaticCall())
            staticRootStatus = parentStatus;

        context = ExecutionContext(trace, context);

        var subtraces = trace.OptionalArray("calls")?.EnumerateArray().ToList() ?? [];
        var subtraceLogs = new List<List<JsonElement>>(subtraces.Count);
        var ownLogs = new List<JsonElement>();

        var pos = 0;
        foreach (var subtrace in subtraces)
        {
            var end = Math.Clamp(subtrace.OptionalArray("logs")?.GetArrayLength() ?? 0, pos, subtreeLogs.Count);
            var start = end;

            if (start > pos && !subtrace.HasFailed() && !subtrace.IsStaticCall())
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

        yield return new TraceFrame
        {
            Trace = trace,
            Depth = depth,
            Status = status,
            ParentStatus = parentStatus,
            StaticRootStatus = staticRootStatus,
            Logs = ownLogs,
        };

        for (int i = 0; i < subtraces.Count; i++)
            foreach (var item in EnumerateTraces(subtraces[i], subtraceLogs[i], context, depth + 1, status, staticRootStatus))
                yield return item;
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

    readonly struct TraceFrame
    {
        public required JsonElement Trace { get; init; }
        public required int Depth { get; init; }
        public required OperationStatus Status { get; init; }
        public required OperationStatus ParentStatus { get; init; }
        public required OperationStatus? StaticRootStatus { get; init; }
        public required List<JsonElement> Logs { get; init; }
    }
    #endregion
}
