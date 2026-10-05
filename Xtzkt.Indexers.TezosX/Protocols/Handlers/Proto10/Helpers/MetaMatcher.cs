using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10.Helpers;

public partial class ProtoHelpers
{
    static readonly BigInteger WeiPerMutez = BigInteger.Pow(10, 12);

    void MatchEvmOp(EvmOpContext context)
    {
        // for deposits there is nothing to match
        if (context.Operation.From == EvmRuntime.DepositOrigin)
        {
            if (context.MichelsonContents != null)
                throw new Exception($"Unexpected cross-runtime calls in deposit {context.Operation.Batch.Hash}");

            return;
        }

        AlignCracFrames(GetOuterGatewayFrames(context.RootFrame), context.MichelsonContents ?? []);
    }

    void MatchMichelsonOp(MichelsonOpContext context)
    {
        // for deposits there is nothing to match
        if (context.Contents.Any(x => x.Op.From == MichelsonRuntime.DepositOrigin))
        {
            if (context.EvmRoot != null)
                throw new Exception($"Unexpected cross-runtime calls in deposit {context.Batch.Hash}");

            return;
        }

        var isApplied = context.Contents.All(x => GetStatus(x.Op) == OperationStatus.Applied);

        if (context.EvmRoot is not EvmRootFrame root)
        {
            // kernel drops evm side of a backtracked operation so there is nothing to match with
            // TODO: revisit once the kernel reworks the evm side of backtracked operations
            if (isApplied && context.Contents.Any(HasCracCalls))
                throw new Exception($"Synthetic transaction of operation {context.Batch.Hash} missed");

            return;
        }

        if (!isApplied)
            throw new Exception($"Unexpected synthetic transaction of backtracked operation {context.Batch.Hash}");

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
            MatchCracFrames(content.Internals, content.EvmTarget);
    }

    List<EvmFrame> GetOuterGatewayFrames(EvmFrame frame)
    {
        if (IsCracCall(frame))
            return [frame];

        var res = new List<EvmFrame>();
        CollectGatewayFrames(frame, res);
        return res;
    }

    void CollectGatewayFrames(EvmFrame frame, List<EvmFrame> dest)
    {
        foreach (var call in frame.Calls)
        {
            if (IsCracCall(call))
                dest.Add(call);
            else
                CollectGatewayFrames(call, dest);
        }
    }

    void AlignCracFrames(List<EvmFrame> gatewayCalls, List<MichelsonCracFrameContent> cracFrames)
    {
        if (gatewayCalls.Count == 0 && cracFrames.Count == 0)
            return;

        var pairs = new List<(EvmFrame, MichelsonCracFrameContent)>();

        if (!new CracAligner(this, gatewayCalls, cracFrames).TryCollect(0, pairs, out var ambiguous))
            throw new Exception("Failed to match cross-runtime calls");

        if (ambiguous)
            Logger.LogWarning("Cross-runtime calls of {hash} match ambiguously", gatewayCalls[0].Hash);

        foreach (var (gatewayCall, cracContent) in pairs)
        {
            gatewayCall.MichelsonTarget = cracContent;
            // the aligner pairs only the calls IsCracTarget accepts, so this can't fail
            MatchEvmTargets(gatewayCall, cracContent, apply: true);
            MatchCracFrames(cracContent.Internals, null);
        }
    }

    void MatchCracFrames(List<MichelsonInternalContent> contents, EvmInternalFrame? topLevelTarget)
    {
        // crac frames entered from the target of an internal call are spliced right after that call,
        // crac frames entered from the target of a top-level call are appended after all internal operations
        // TODO: re-check it after release of Ganesha 7.3 with fixes
        List<EvmFrame>? gatewayCalls = null;
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
            gatewayCalls = null;
            if (content is MichelsonInternalOpContent { Op: var internalOp } internalOpContent && IsCracCall(internalOp))
                gatewayCalls = internalOpContent.EvmTarget is EvmInternalFrame target ? GetOuterGatewayFrames(target) : [];
        }

        if (topLevelTarget != null)
            (gatewayCalls ??= []).AddRange(GetOuterGatewayFrames(topLevelTarget));

        Align();

        void Align()
        {
            if (gatewayCalls != null)
                AlignCracFrames(gatewayCalls, cracFrames);
            else if (cracFrames.Count != 0)
                throw new Exception("Unexpected crac frame");
        }
    }

    bool MatchEvmTargets(EvmFrame gatewayCall, MichelsonCracFrameContent cracFrame, bool apply)
    {
        var targets = new EvmTargets(this, gatewayCall);

        var targetCall = cracFrame.TargetCall;
        if (IsCracCall(targetCall))
        {
            if (!targets.TryGetNext(targetCall.From, GetStatus(targetCall), targetCall.Content, out var target))
                return false;

            if (apply)
                cracFrame.EvmTarget = target;
        }

        foreach (var content in cracFrame.Internals)
        {
            if (content is not MichelsonInternalOpContent { Op: var op } opContent || !IsCracCall(op))
                continue;

            if (!targets.TryGetNext(op.From, GetStatus(op), op.Content, out var target))
                return false;

            if (apply)
                opContent.EvmTarget = target;
        }

        return targets.IsConsumed;
    }

    bool IsCracCall(EvmFrame frame)
    {
        return frame is not EvmInternalFrame { Op.StaticRootStatus: not null } && EvmRuntime.IsCracCall(frame.To, frame.Trace);
    }

    bool IsCracCall(MichelsonOperation op)
    {
        return MichelsonRuntime.IsCracCall(op.To, op.Content);
    }

    bool IsCracCall(MichelsonInternalOperation op)
    {
        return MichelsonRuntime.IsCracCall(op.To, op.Content);
    }

    bool HasCracCalls(MichelsonContent content)
    {
        return IsCracCall(content.Op) ||
            content.Internals.Any(x => x is MichelsonInternalOpContent { Op: var op } && IsCracCall(op));
    }

    bool IsCracTarget(EvmFrame gatewayCall, MichelsonCracFrameContent cracFrame)
    {
        var targetCall = cracFrame.TargetCall;
        var sender = gatewayCall.From;
        if (targetCall.From != MichelsonRuntime.GetAlias(sender) && EvmRuntime.GetAlias(targetCall.From) != sender)
            return false;

        if (GetMichelsonCall(gatewayCall) is not MichelsonCall call ||
            targetCall.To != call.Address ||
            GetEntrypoint(targetCall) != call.Entrypoint ||
            targetCall.Content.RequiredBigInteger("amount") != call.Amount)
            return false;

        var status = GetStatus(cracFrame.Begin);
        var fits = gatewayCall.Status switch
        {
            OperationStatus.Applied => status == OperationStatus.Applied,
            OperationStatus.Backtracked => status == OperationStatus.Backtracked && !cracFrame.HasFailure,
            OperationStatus.Failed => status == OperationStatus.Backtracked,
            _ => false,
        };

        return fits && MatchEvmTargets(gatewayCall, cracFrame, apply: false);
    }

    static bool CanEnterNothing(EvmFrame gatewayCall)
    {
        return gatewayCall.Status == OperationStatus.Failed && gatewayCall.Calls.Count == 0;
    }

    static string GetEntrypoint(MichelsonInternalOperation op)
    {
        return op.Content.Optional("parameters")?.RequiredString("entrypoint") ?? "default";
    }

    MichelsonCall? GetMichelsonCall(EvmFrame gateway)
    {
        if (!gateway.IsMichelsonCallParsed)
        {
            gateway.MichelsonCall = ParseMichelsonCall(gateway.Trace);
            gateway.IsMichelsonCallParsed = true;
        }
        return gateway.MichelsonCall;
    }

    MichelsonCall? ParseMichelsonCall(JsonElement trace)
    {
        if (trace.OptionalHexBytes("input") is not byte[] input || input.Length < 4)
            return null;

        var amount = trace.RequiredHexBigInteger("value") / WeiPerMutez;

        try
        {
            var selector = Hex.GetString(input.AsSpan(0, 4));
            var args = new AbiReader(input[4..]);

            string url;
            if (selector.Equals(EvmRuntime.CallMichelsonSelector, StringComparison.OrdinalIgnoreCase))
            {
                // callMichelson(string destination, string entrypoint, bytes parameters)
                var destination = args.ReadString(0);
                var entrypoint = args.ReadString(1);
                url = entrypoint.Length == 0
                    ? $"http://tezos/{destination}"
                    : $"http://tezos/{destination}/{entrypoint}";
            }
            else if (selector.Equals(EvmRuntime.CallSelector, StringComparison.OrdinalIgnoreCase))
            {
                // call(string url, (string, string)[] headers, bytes body, uint8 method)
                url = args.ReadString(0);
            }
            else
            {
                return null;
            }

            // ensure the third arg is decodable
            args.ReadBytes(2);

            return ParseTezosUrl(url) is { } target ? new(target.Address, target.Entrypoint, amount) : null;
        }
        catch (FormatException)
        {
            // such an input doesn't make it past the gateway
            return null;
        }
    }

    static (string Address, string Entrypoint)? ParseTezosUrl(string url)
    {
        if (!TrySplitUrl(url, out var host, out var path) || host != "tezos" || path is not ['/', _, ..])
            return null;

        var rest = path[1..];
        var slash = rest.IndexOf('/');
        if (slash < 0)
            return (rest, "default");

        return (rest[..slash], slash == rest.Length - 1 ? "default" : rest[(slash + 1)..]);
    }

    static bool TrySplitUrl(string url, out string host, out string path)
    {
        host = path = "";

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
            return false;

        var rest = url.AsSpan(schemeEnd + 3);
        var authorityEnd = rest.IndexOfAny('/', '?', '#');
        var authority = authorityEnd < 0 ? rest : rest[..authorityEnd];
        if (authority.LastIndexOf('@') is var at and >= 0)
            authority = authority[(at + 1)..];
        if (authority.LastIndexOf(':') is var colon and >= 0)
            authority = authority[..colon];
        host = authority.ToString();

        if (authorityEnd >= 0 && rest[authorityEnd] == '/')
        {
            var tail = rest[authorityEnd..];
            var pathEnd = tail.IndexOfAny('?', '#');
            path = (pathEnd < 0 ? tail : tail[..pathEnd]).ToString();
        }
        return true;
    }

    static string? ParseEthereumUrl(string url)
    {
        if (!TrySplitUrl(url, out var host, out var path) || host != "ethereum" || path is not ['/', _, ..])
            return null;

        var address = path[1..].TrimEnd('/');
        if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            address = address[2..];

        return address.Length == 40 && address.All(char.IsAsciiHexDigit) ? $"0x{address.ToLowerInvariant()}" : null;
    }

    bool IsCracTarget(string sender, JsonElement content, EvmInternalOperation target)
    {
        // incoming static calls are pruned from the traces
        if (target.Trace.IsStaticCall())
            return false;

        if (target.To == null)
            return false;

        if (target.From != EvmRuntime.GetAlias(sender) && MichelsonRuntime.GetAlias(target.From) != sender)
            return false;

        var parameters = content.Required("parameters");
        var entrypoint = parameters.RequiredString("entrypoint");
        var value = parameters.Required("value");

        string? address;
        byte[]? input;
        switch (entrypoint)
        {
            case "call_evm":
                // (destination, (method signature, (abi parameters, callback)))
                address = Micheline.GetCombString(value, 0) is string destination
                    ? ParseEthereumUrl($"http://ethereum/{destination}")
                    : null;
                input = Micheline.GetCombString(value, 1) is string signature && Micheline.GetCombBytes(value, 2) is byte[] args
                    ? [.. Keccak256.GetHashBytes(System.Text.Encoding.UTF8.GetBytes(signature)).AsSpan(0, 4), .. args]
                    : null;
                break;
            case "call":
                // (url, (headers, (body, (method, callback))))
                address = Micheline.GetCombString(value, 0) is string url ? ParseEthereumUrl(url) : null;
                input = Micheline.GetCombBytes(value, 2);
                break;
            default:
                throw new Exception($"Unexpected gateway entrypoint {entrypoint}");
        }

        return address == target.To
            && input != null && input.AsSpan().SequenceEqual(target.Trace.OptionalHexBytes("input") ?? [])
            && target.Trace.RequiredHexBigInteger("value") == content.RequiredInt64("amount") * WeiPerMutez;
    }

    static OperationStatus GetStatus(MichelsonOperation op)
    {
        return op.Content.Required("metadata").Required("operation_result").RequiredOpStatus("status");
    }

    static OperationStatus GetStatus(MichelsonInternalOperation op)
    {
        return op.Content.Required("result").RequiredOpStatus("status");
    }

    sealed class EvmTargets(ProtoHelpers helpers, EvmFrame root)
    {
        public bool IsConsumed => _next == root.Calls.Count;

        int _next;

        public bool TryGetNext(string sender, OperationStatus status, JsonElement content, out EvmInternalFrame? target)
        {
            target = null;

            if (status == OperationStatus.Skipped)
                return true;

            if (_next == root.Calls.Count)
                return status == OperationStatus.Failed;

            if (!helpers.IsCracTarget(sender, content, root.Calls[_next].Op))
                return false;

            target = root.Calls[_next++];
            return true;
        }

        public EvmInternalFrame? GetNext(string sender, OperationStatus status, JsonElement content)
        {
            if (!TryGetNext(sender, status, content, out var target))
                throw new Exception("Unexpected cross-runtime call target");

            return target;
        }

        public void EnsureConsumed()
        {
            if (!IsConsumed)
                throw new Exception("Not all cross-runtime call targets were matched");
        }
    }

    sealed class CracAligner
    {
        readonly ProtoHelpers _helpers;
        readonly List<EvmFrame> _gatewayCalls;
        readonly CracFrames _cracFrames;
        readonly int _to;
        readonly int _words;

        readonly ulong[]?[] _entered;
        readonly List<(int Crac, CracAligner Inner)>?[] _passed;
        readonly ulong[][] _reach;

        public CracAligner(ProtoHelpers helpers, List<EvmFrame> gatewayCalls, List<MichelsonCracFrameContent> cracFrames)
            : this(helpers, gatewayCalls, new CracFrames(cracFrames), cracFrames.Count) { }

        CracAligner(ProtoHelpers helpers, List<EvmFrame> gatewayCalls, CracFrames cracFrames, int to)
        {
            _helpers = helpers;
            _gatewayCalls = gatewayCalls;
            _cracFrames = cracFrames;
            _to = to;
            _words = (to >> 6) + 1;

            _entered = new ulong[]?[gatewayCalls.Count];
            _passed = new List<(int, CracAligner)>?[gatewayCalls.Count];
            _reach = new ulong[gatewayCalls.Count + 1][];

            var similar = new Dictionary<(string, MichelsonCall, OperationStatus), ulong[]?>();

            _reach[gatewayCalls.Count] = new ulong[_words];
            Set(_reach[gatewayCalls.Count], to);

            for (var i = gatewayCalls.Count - 1; i >= 0; i--)
            {
                var gatewayCall = gatewayCalls[i];
                var next = _reach[i + 1];
                if (helpers.GetMichelsonCall(gatewayCall) is MichelsonCall call &&
                    cracFrames.Index.TryGetValue(call, out var positions))
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
                        {
                            if (pos >= to)
                                break;

                            var crac = cracFrames.Frames[pos];
                            if (!Get(next, pos + 1) || !helpers.IsCracTarget(gatewayCall, crac))
                                continue;

                            // a target calling the gateway again is a pass-through, its inner frames precede it
                            if (helpers.IsCracCall(crac.TargetCall))
                                (_passed[i] ??= []).Add((pos, cracFrames.GetInner(helpers, gatewayCall.Calls[0], pos)));
                            else
                                Set(_entered[i] ??= new ulong[_words], pos);
                        }
                    }
                }

                if (_entered[i] == null && _passed[i] == null)
                {
                    _reach[i] = CanEnterNothing(gatewayCall) ? next : new ulong[_words];
                    continue;
                }

                var reach = CanEnterNothing(gatewayCall) ? (ulong[])next.Clone() : new ulong[_words];

                if (_entered[i] is ulong[] entered)
                    for (var w = 0; w < _words; w++)
                        reach[w] |= entered[w] & (next[w] >> 1 | (w + 1 < _words ? next[w + 1] << 63 : 0));

                if (_passed[i] is { } passed)
                    foreach (var (p, inner) in passed)
                        if (Get(next, p + 1))
                            for (var w = 0; w < inner._words; w++)
                                reach[w] |= inner._reach[0][w];

                _reach[i] = reach;
            }
        }

        public bool TryCollect(int start, List<(EvmFrame, MichelsonCracFrameContent)> pairs, out bool ambiguous)
        {
            ambiguous = false;
            if (!Get(_reach[0], start))
                return false;

            var j = start;
            for (var i = 0; i < _gatewayCalls.Count; i++)
            {
                var next = _reach[i + 1];
                (int Crac, CracAligner? Inner)? choice = null;
                var choices = 0;

                if (j < _to && _entered[i] is ulong[] entered && Get(entered, j) && Get(next, j + 1))
                {
                    choice = (j, null);
                    choices++;
                }

                if (_passed[i] is { } passed)
                {
                    foreach (var (p, inner) in passed)
                    {
                        if (p >= j && Get(next, p + 1) && Get(inner._reach[0], j))
                        {
                            choice ??= (p, inner);
                            choices++;
                        }
                    }
                }

                if (CanEnterNothing(_gatewayCalls[i]) && Get(next, j))
                {
                    choice ??= (-1, null);
                    choices++;
                }

                ambiguous |= choices > 1;
                var (crac, innerChoice) = choice!.Value;
                if (crac < 0)
                    continue;

                if (innerChoice != null)
                {
                    innerChoice.TryCollect(j, pairs, out var innerAmbiguous);
                    ambiguous |= innerAmbiguous;
                }
                pairs.Add((_gatewayCalls[i], _cracFrames.Frames[crac]));
                j = crac + 1;
            }
            return true;
        }

        ulong[]? Enter(EvmFrame gatewayCall, List<int> positions)
        {
            ulong[]? entered = null;
            foreach (var pos in positions)
            {
                if (pos >= _to)
                    break;

                if (_helpers.IsCracTarget(gatewayCall, _cracFrames.Frames[pos]))
                    Set(entered ??= new ulong[_words], pos);
            }
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

        sealed class CracFrames
        {
            public List<MichelsonCracFrameContent> Frames { get; }
            public Dictionary<MichelsonCall, List<int>> Index { get; } = [];

            readonly Dictionary<(EvmInternalFrame, int), CracAligner> _inner = [];

            public CracFrames(List<MichelsonCracFrameContent> frames)
            {
                Frames = frames;
                for (var pos = 0; pos < frames.Count; pos++)
                {
                    var targetCall = frames[pos].TargetCall;
                    var key = new MichelsonCall(
                        targetCall.To!,
                        GetEntrypoint(targetCall),
                        targetCall.Content.RequiredBigInteger("amount"));

                    if (!Index.TryGetValue(key, out var positions))
                        Index.Add(key, positions = []);

                    positions.Add(pos);
                }
            }

            public CracAligner GetInner(ProtoHelpers helpers, EvmInternalFrame target, int to)
            {
                if (!_inner.TryGetValue((target, to), out var inner))
                    _inner.Add((target, to), inner = new CracAligner(helpers, helpers.GetOuterGatewayFrames(target), this, to));

                return inner;
            }
        }
    }
}
