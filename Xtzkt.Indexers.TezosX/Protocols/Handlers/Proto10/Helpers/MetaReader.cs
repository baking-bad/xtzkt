using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10.Helpers;

public partial class ProtoHelpers
{
    MetaBatch? ReadOperation(MetaBlockContext context, string hash, bool delayed)
    {
        MetaBatch batch;
        if (context.EvmOps.Remove(hash, out var evmChain))
        {
            batch = new MetaBatch { Delayed = delayed, Hash = Hashes.ParseOperationHash(hash) };
            ReadEvmOperation(context, evmChain, batch);
        }
        else if (context.MichelsonOps.Remove(hash, out var michelsonChain))
        {
            batch = new MetaBatch { Delayed = delayed, Hash = Hashes.ParseOperationHash(hash) };
            ReadMichelsonOperation(context, michelsonChain, batch);
        }
        else
        {
            // operation from blueprint can be dropped and not appear in the block at all
            return null;
        }
        return batch;
    }

    void ReadEvmOperation(MetaBlockContext blockContext, EvmOpContext opContext, MetaBatch dest)
    {
        var op = opContext.Operation;

        if (opContext.RootFrame.MichelsonTarget is MichelsonCracFrameContent crac)
        {
            var operation = new CracOperation { GatewayCall = op, TargetCall = crac.TargetCall };
            dest.Operations.Add(operation);
            ReadMichelsonCracContent(dest, crac, operation);
        }
        else if (op.From == EvmRuntime.DepositOrigin)
        {
            var deposit = blockContext.DelayedOps.FirstOrDefault(x => x.Hash == op.Batch.Hash);
            if (deposit is not (DelayedXtzDeposit or DelayedFaDeposit))
                throw new Exception("Operation from the deposit origin doesn't match any delayed deposit");

            var bridgeCalls = EnumerateSubcalls(opContext.RootFrame);
            dest.Operations.Add(new EvmDeposit { Deposit = deposit, FeederCall = op, BridgeCalls = [.. bridgeCalls] });
        }
        else
        {
            dest.Operations.Add(op);
            ReadEvmFrameSubcalls(dest, opContext.RootFrame, null);
        }
    }

    void ReadMichelsonOperation(MetaBlockContext blockContext, MichelsonOpContext opContext, MetaBatch dest)
    {
        foreach (var content in opContext.Contents)
        {
            var op = content.Op;

            if (content.EvmTarget is EvmInternalFrame crac)
            {
                var operation = new CracOperation { GatewayCall = op, TargetCall = crac.Op };
                dest.Operations.Add(operation);
                ReadEvmCracFrame(dest, crac, operation);
                ReadMichelsonInternalContent(dest, content.Internals, null);
            }
            else if (op.From == MichelsonRuntime.DepositOrigin)
            {
                var deposit = blockContext.DelayedOps.FirstOrDefault(x => x.Hash == op.Batch.Hash);
                if (deposit is not (DelayedXtzDeposit or DelayedFaDeposit))
                    throw new Exception("Operation from the deposit origin doesn't match any delayed deposit");

                var bridgeCalls = content.Internals.Cast<MichelsonInternalOpContent>().Select(x => x.Op);
                dest.Operations.Add(new MichelsonDeposit { Deposit = deposit, FeederCall = op, BridgeCalls = [.. bridgeCalls] });
            }
            else
            {
                dest.Operations.Add(op);
                ReadMichelsonInternalContent(dest, content.Internals, null);
            }
        }
    }

    void ReadMichelsonCracContent(MetaBatch dest, MichelsonCracFrameContent cracContent, MetaContent cracOperation)
    {
        if (cracContent.EvmTarget is EvmInternalFrame target) // the target call is itself a call to the evm gateway
        {
            var inner = new InternalCracOperation { GatewayCall = cracContent.TargetCall, TargetCall = target.Op, CracParent = cracOperation };
            dest.Operations[^1].Internals.Add(inner);
            ReadEvmCracFrame(dest, target, inner);
        }

        ReadMichelsonInternalContent(dest, cracContent.Internals, cracOperation);
    }

    void ReadMichelsonInternalContent(MetaBatch dest, List<MichelsonInternalContent> contents, MetaContent? cracParent)
    {
        foreach (var content in contents)
        {
            // crac frames are read along with the gateway calls that entered them, while the ones of a backtracked
            // operation are skipped, as the kernel drops its evm side and there is nothing to match them with
            // TODO: revisit once the kernel reworks the evm side of backtracked operations
            if (content is not MichelsonInternalOpContent { Op: var op } opContent)
                continue;

            if (opContent.EvmTarget is EvmInternalFrame target)
            {
                var operation = new InternalCracOperation { GatewayCall = op, TargetCall = target.Op, CracParent = cracParent };
                dest.Operations[^1].Internals.Add(operation);
                ReadEvmCracFrame(dest, target, operation);
            }
            else
            {
                op.CracParent = cracParent;
                dest.Operations[^1].Internals.Add(op);
            }
        }
    }

    void ReadEvmCracFrame(MetaBatch dest, EvmInternalFrame cracFrame, MetaContent operation)
    {
        if (cracFrame.MichelsonTarget != null) // the target is itself a call to the michelson gateway
            ReadEvmFrame(dest, cracFrame, operation);
        else
            ReadEvmFrameSubcalls(dest, cracFrame, operation);
    }

    void ReadEvmFrame(MetaBatch dest, EvmInternalFrame frame, MetaContent? cracParent)
    {
        var op = frame.Op;

        if (frame.MichelsonTarget is MichelsonCracFrameContent crac)
        {
            var operation = new InternalCracOperation { GatewayCall = op, TargetCall = crac.TargetCall, CracParent = cracParent };
            dest.Operations[^1].Internals.Add(operation);
            ReadMichelsonCracContent(dest, crac, operation);
        }
        else
        {
            op.CracParent = cracParent;
            dest.Operations[^1].Internals.Add(op);
            ReadEvmFrameSubcalls(dest, frame, cracParent);
        }
    }

    void ReadEvmFrameSubcalls(MetaBatch dest, EvmFrame frame, MetaContent? cracParent)
    {
        foreach (var call in frame.Calls)
            ReadEvmFrame(dest, call, cracParent);
    }

    static IEnumerable<EvmInternalOperation> EnumerateSubcalls(EvmFrame frame)
    {
        foreach (var call in frame.Calls)
        {
            yield return call.Op;
            foreach (var descendant in EnumerateSubcalls(call))
                yield return descendant;
        }
    }

    sealed class MetaBlockContext
    {
        public required List<DelayedOperation> DelayedOps { get; init; }
        public required Dictionary<string, EvmOpContext> EvmOps { get; init; }
        public required Dictionary<string, MichelsonOpContext> MichelsonOps { get; init; }
    }

    sealed class EvmOpContext
    {
        public required EvmOperation Operation { get; init; }
        public required EvmRootFrame RootFrame { get; init; }

        public List<MichelsonCracFrameContent>? MichelsonContents { get; set; }
    }

    sealed class MichelsonOpContext
    {
        public required MichelsonBatch Batch { get; init; }
        public required List<MichelsonContent> Contents { get; init; }

        public EvmRootFrame? EvmRoot { get; set; }
    }

    abstract class EvmFrame
    {
        public abstract string Hash { get; }
        public abstract string From { get; }
        public abstract string? To { get; }
        public abstract JsonElement Trace { get; }
        public abstract OperationStatus Status { get; }

        public List<EvmInternalFrame> Calls { get; } = [];

        // set for gateway calls
        public MichelsonCracFrameContent? MichelsonTarget { get; set; }
        public MichelsonCall? MichelsonCall { get; set; }
        public bool IsMichelsonCallParsed { get; set; }
    }

    sealed class EvmRootFrame : EvmFrame
    {
        public required EvmOperation Op { get; init; }

        public override string Hash => Op.Batch.Hash;
        public override string From => Op.From;
        public override string? To => Op.To;
        public override JsonElement Trace => Op.Trace;
        public override OperationStatus Status => Op.Trace.TraceStatus();
    }

    sealed class EvmInternalFrame : EvmFrame
    {
        public required EvmInternalOperation Op { get; init; }

        public override string Hash => Op.Operation.Batch.Hash;
        public override string From => Op.From;
        public override string? To => Op.To;
        public override JsonElement Trace => Op.Trace;
        public override OperationStatus Status => Op.Status;
    }

    sealed class MichelsonContent
    {
        public required MichelsonOperation Op { get; init; }
        public required List<MichelsonInternalContent> Internals { get; init; }

        // set for gateway calls
        public EvmInternalFrame? EvmTarget { get; set; }
    }

    abstract class MichelsonInternalContent;

    sealed class MichelsonInternalOpContent : MichelsonInternalContent
    {
        public required MichelsonInternalOperation Op { get; init; }

        // set for gateway calls
        public EvmInternalFrame? EvmTarget { get; set; }
    }

    sealed class MichelsonCracFrameContent : MichelsonInternalContent
    {
        public required MichelsonInternalOperation Begin { get; init; }
        public required MichelsonInternalOperation TargetCall { get; init; }
        public required List<MichelsonInternalContent> Internals { get; init; }
        public required bool HasFailure { get; init; }

        // set for gateway calls
        public EvmInternalFrame? EvmTarget { get; set; }
    }

    // the call to the michelson runtime made by a call to the michelson gateway
    sealed record MichelsonCall(string Address, string Entrypoint, BigInteger Amount);
}
