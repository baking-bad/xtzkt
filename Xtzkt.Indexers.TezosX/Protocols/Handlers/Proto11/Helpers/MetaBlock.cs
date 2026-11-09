using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11.Helpers;

public partial class ProtoHelpers
{
    public override bool IsDroppedFrame(JsonElement trace)
    {
        return trace.RequiredString("from") == EvmRuntime.TezosXCaller && !HasCalls(trace);
    }

    protected override EvmRootFrame GetEvmRootFrame(EvmOperation op, bool isSynthetic)
    {
        var root = new EvmRootFrame { Op = op };
        AddEvmFrames(op, root, isSynthetic || EvmRuntime.IsCracCall(op.To, op.Trace), 0, root.Status, null);
        return root;
    }

    void AddEvmFrames(EvmOperation op, EvmFrame frame, bool isCracFrameRoot, int depth, OperationStatus status, OperationStatus? staticRootStatus)
    {
        if (frame.Trace.OptionalArray("calls") is not JsonElement calls)
            return;

        foreach (var call in calls.EnumerateArray())
        {
            // skip dropped frames
            if (IsDroppedFrame(call))
            {
                #region debug
                if (call.OptionalHexBigInteger("value") is { IsZero: false })
                    throw new Exception("Unexpected value in dropped frame");
                #endregion
                continue;
            }

            // incoming cross-runtime static trees have no gateway call to be matched with
            if (isCracFrameRoot && call.IsStaticCall())
            {
                #region debug
                if (HasLogs(call))
                    throw new Exception("Unexpected logs in a static call");
                #endregion
                continue;
            }

            var iop = new EvmInternalOperation
            {
                Operation = op,
                Depth = depth + 1,
                Trace = call,
                Logs = call.OptionalArray("logs")?.EnumerateArray().ToList() ?? [],
                Status = call.TraceStatus(status),
                ParentStatus = status,
                StaticRootStatus = staticRootStatus ?? (call.IsStaticCall() ? status : null),
                From = call.RequiredString("from"),
                To = call.OptionalString("to"),
            };

            var internalFrame = new EvmInternalFrame { Op = iop };
            frame.Calls.Add(internalFrame);

            AddEvmFrames(op, internalFrame, EvmRuntime.IsCracCall(iop.To, call), depth + 1, iop.Status, iop.StaticRootStatus);
        }
    }

    protected override List<MichelsonInternalContent> GetMichelsonInternalContents(MichelsonOperation op)
    {
        var iops = GetMichelsonInternalOperations(op);
        var pos = 0;
        var items = GetMichelsonInternalContents(iops, ref pos, IsAliasMaterialization(op.To, op.Content));
        if (pos != iops.Count)
            throw new Exception("Unexpected crac event");
        return items;
    }

    List<MichelsonInternalContent> GetMichelsonInternalContents(List<MichelsonInternalOperation> iops, ref int pos, bool materializesAlias)
    {
        var items = new List<MichelsonInternalContent>();

        if (materializesAlias)
            SkipAliasOriginations(iops, ref pos);

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
                    if (IsAliasMaterialization(iop.To, iop.Content))
                        SkipAliasOriginations(iops, ref pos);
                    break;
            }
        }
        return items;
    }

    MichelsonCracFrameContent GetMichelsonCracContent(List<MichelsonInternalOperation> iops, ref int pos)
    {
        // skip crac begin event
        var begin = iops[pos++];

        SkipAliasOriginations(iops, ref pos);

        if (pos == iops.Count ||
            iops[pos].Content.RequiredString("kind") != "transaction" ||
            iops[pos].To == null)
            throw new Exception("Crac frame target call missed");

        // take target call
        var targetCall = iops[pos++];

        // take internal ops
        var items = GetMichelsonInternalContents(iops, ref pos, IsAliasMaterialization(targetCall.To, targetCall.Content));

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

    bool IsAliasMaterialization(string? to, JsonElement content)
    {
        return to == MichelsonRuntime.EvmGateway &&
            content.Optional("parameters")?.RequiredString("entrypoint") == "materialize_alias";
    }

    void SkipAliasOriginations(List<MichelsonInternalOperation> iops, ref int pos)
    {
        while (pos < iops.Count &&
            iops[pos].Content.RequiredString("kind") == "origination" &&
            iops[pos].From == MichelsonRuntime.NullAddress)
            pos++;
    }
}
