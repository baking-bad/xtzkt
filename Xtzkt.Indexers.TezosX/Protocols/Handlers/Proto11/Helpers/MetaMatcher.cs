using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11.Helpers;

public partial class ProtoHelpers
{
    protected override void MatchMichelsonOp(MichelsonOpContext context)
    {
        // for deposits there is nothing to match
        if (context.Contents.Any(x => x.Op.From == MichelsonRuntime.DepositOrigin))
        {
            if (context.EvmRoot != null)
                throw new Exception($"Unexpected cross-runtime calls in deposit {context.Batch.Hash}");

            return;
        }

        if (context.EvmRoot is not EvmRootFrame root)
        {
            if (context.Contents.Any(HasExecutedCracCalls))
                throw new Exception($"Synthetic transaction of operation {context.Batch.Hash} missed");

            return;
        }

        #region debug
        var isApplied = context.Contents.All(x => GetStatus(x.Op) == OperationStatus.Applied);
        if (isApplied != (root.Status == OperationStatus.Applied))
            throw new Exception($"Unexpected status of synthetic transaction of operation {context.Batch.Hash}");
        #endregion

        // match direct calls
        var targets = new EvmTargets(this, root);
        foreach (var content in context.Contents)
        {
            if (IsCracCall(content.Op))
                content.EvmTarget = targets.GetNext(content.Op.From, GetStatus(content.Op), content.Op.Content);

            foreach (var internalContent in content.Internals)
                if (internalContent is MichelsonInternalOpContent { Op: var internalOp } internalOpContent && IsCracCall(internalOp))
                    internalOpContent.EvmTarget = targets.GetNext(internalOp.From, GetStatus(internalOp), internalOp.Content);
        }
        targets.EnsureConsumed();

        // match re-entrancy
        foreach (var content in context.Contents)
            MatchCracFrames(content.Internals, IsCracCall(content.Op) ? GetEnteredGatewayFrames(content.EvmTarget) : null);
    }

    protected virtual void MatchCracFrames(List<MichelsonInternalContent> contents, List<EvmFrame>? leadingGatewayCalls)
    {
        var gatewayCalls = leadingGatewayCalls;
        List<MichelsonCracFrameContent> cracFrames = [];

        foreach (var content in contents)
        {
            if (content is MichelsonCracFrameContent cracFrame)
            {
                cracFrames.Add(cracFrame);
                continue;
            }

            Align();

            cracFrames = [];
            gatewayCalls = content is MichelsonInternalOpContent { Op: var internalOp } internalOpContent && IsCracCall(internalOp)
                ? GetEnteredGatewayFrames(internalOpContent.EvmTarget)
                : null;
        }

        Align();

        void Align()
        {
            if (gatewayCalls != null)
                AlignCracFrames(gatewayCalls, cracFrames);
            else if (cracFrames.Count != 0)
                throw new Exception("Unexpected crac frame");
        }
    }

    List<EvmFrame> GetEnteredGatewayFrames(EvmInternalFrame? target)
    {
        // a failed call may have been rejected before reaching its target
        return target != null ? GetOuterGatewayFrames(target) : [];
    }

    protected override void AlignCracFrames(List<EvmFrame> gatewayCalls, List<MichelsonCracFrameContent> cracFrames)
    {
        if (gatewayCalls.Count == 0 && cracFrames.Count == 0)
            return;

        var pairs = new List<(EvmFrame, MichelsonCracFrameContent)>();

        if (!new CracAligner(this, gatewayCalls, cracFrames).TryCollect(pairs, out var ambiguous))
            throw new Exception("Failed to match cross-runtime calls");

        if (ambiguous)
            Logger.LogWarning("Cross-runtime calls of {hash} match ambiguously", gatewayCalls[0].Hash);

        foreach (var (gatewayCall, cracContent) in pairs)
        {
            gatewayCall.MichelsonTarget = cracContent;
            // the aligner pairs only the calls IsCracTarget accepts, so this can't fail
            MatchEvmTargets(gatewayCall, cracContent, apply: true);
            MatchCracFrames(cracContent.Internals, IsCracCall(cracContent.TargetCall) ? GetEnteredGatewayFrames(cracContent.EvmTarget) : null);
        }
    }

    bool HasExecutedCracCalls(MichelsonContent content)
    {
        return IsCracCall(content.Op) && GetStatus(content.Op) is OperationStatus.Applied or OperationStatus.Backtracked ||
            content.Internals.Any(x => x is MichelsonCracFrameContent) ||
            content.Internals.Any(x => x is MichelsonInternalOpContent { Op: var op } && IsCracCall(op) &&
                GetStatus(op) is OperationStatus.Applied or OperationStatus.Backtracked);
    }

    sealed class CracAligner
    {
        readonly ProtoHelpers _helpers;
        readonly List<EvmFrame> _gatewayCalls;
        readonly List<MichelsonCracFrameContent> _cracFrames;
        readonly int _words;

        readonly ulong[]?[] _entered;
        readonly ulong[][] _reach;

        public CracAligner(ProtoHelpers helpers, List<EvmFrame> gatewayCalls, List<MichelsonCracFrameContent> cracFrames)
        {
            _helpers = helpers;
            _gatewayCalls = gatewayCalls;
            _cracFrames = cracFrames;
            _words = (cracFrames.Count >> 6) + 1;

            _entered = new ulong[]?[gatewayCalls.Count];
            _reach = new ulong[gatewayCalls.Count + 1][];

            // frame positions by the michelson call they make
            var index = new Dictionary<MichelsonCall, List<int>>();
            for (var pos = 0; pos < cracFrames.Count; pos++)
            {
                var targetCall = cracFrames[pos].TargetCall;
                var key = new MichelsonCall(targetCall.To!, GetEntrypoint(targetCall), targetCall.Content.RequiredBigInteger("amount"));

                if (!index.TryGetValue(key, out var positions))
                    index.Add(key, positions = []);

                positions.Add(pos);
            }

            var similar = new Dictionary<(string, MichelsonCall, OperationStatus), ulong[]?>();

            _reach[gatewayCalls.Count] = new ulong[_words];
            Set(_reach[gatewayCalls.Count], cracFrames.Count);

            for (var i = gatewayCalls.Count - 1; i >= 0; i--)
            {
                var gatewayCall = gatewayCalls[i];
                var next = _reach[i + 1];
                if (helpers.GetMichelsonCall(gatewayCall) is MichelsonCall call && index.TryGetValue(call, out var positions))
                {
                    if (gatewayCall.Calls.Count == 0)
                    {
                        var key = (gatewayCall.From, call, gatewayCall.Status);
                        if (!similar.TryGetValue(key, out _entered[i]))
                            similar.Add(key, _entered[i] = Enter(gatewayCall, positions));
                    }
                    else
                    {
                        foreach (var pos in positions)
                            if (Get(next, pos + 1) && helpers.IsCracTarget(gatewayCall, cracFrames[pos]))
                                Set(_entered[i] ??= new ulong[_words], pos);
                    }
                }

                var reach = CanEnterNothing(gatewayCall) ? (ulong[])next.Clone() : new ulong[_words];

                if (_entered[i] is ulong[] entered)
                    for (var w = 0; w < _words; w++)
                        reach[w] |= entered[w] & (next[w] >> 1 | (w + 1 < _words ? next[w + 1] << 63 : 0));

                _reach[i] = reach;
            }
        }

        public bool TryCollect(List<(EvmFrame, MichelsonCracFrameContent)> pairs, out bool ambiguous)
        {
            ambiguous = false;
            if (!Get(_reach[0], 0))
                return false;

            var j = 0;
            for (var i = 0; i < _gatewayCalls.Count; i++)
            {
                var next = _reach[i + 1];

                var enters = j < _cracFrames.Count && _entered[i] is ulong[] entered && Get(entered, j) && Get(next, j + 1);
                var skips = CanEnterNothing(_gatewayCalls[i]) && Get(next, j);

                if (!enters && !skips)
                    throw new InvalidOperationException("Cross-runtime call alignment lost its reach");

                ambiguous |= enters && skips;

                if (enters)
                    pairs.Add((_gatewayCalls[i], _cracFrames[j++]));
            }
            return true;
        }

        ulong[]? Enter(EvmFrame gatewayCall, List<int> positions)
        {
            ulong[]? entered = null;
            foreach (var pos in positions)
                if (_helpers.IsCracTarget(gatewayCall, _cracFrames[pos]))
                    Set(entered ??= new ulong[_words], pos);

            return entered;
        }

        static bool Get(ulong[] mask, int pos)
        {
            return (mask[pos >> 6] & (1UL << pos)) != 0;
        }

        static void Set(ulong[] mask, int pos)
        {
            mask[pos >> 6] |= 1UL << pos;
        }
    }
}
