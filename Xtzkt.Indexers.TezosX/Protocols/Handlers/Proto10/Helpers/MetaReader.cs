using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10.Helpers;

public partial class ProtoHelpers
{
    protected virtual MetaBatch? TryReadOperation(MetaContext context, string hash, bool delayed)
    {
        // operation from blueprint can be dropped and not appear in the block at all
        if (!context.QueuesByHash.TryGetValue(hash, out var queue))
            return null;

        var batch = new MetaBatch
        {
            Delayed = delayed,
            Hash = hash,
        };

        ProcessQueue(context, queue, batch);
        return batch;
    }

    protected virtual void ProcessQueue(
        MetaContext context,
        Queue<MetaContent> queue,
        MetaBatch dest,
        Queue<MetaContent>? parentQueue = null,
        int evmDepth = 0,
        bool evmFrameEntered = false,
        MetaContent? cracParent = null)
    {
        while (queue.TryPeek(out var candidate))
        {
            switch (candidate)
            {
                case EvmOperation op:
                    if (parentQueue != null)
                        throw new InvalidOperationException("should never get here");

                    if (EvmRuntime.IsCracCall(op.To, op.Trace))
                    {
                        if (context.QueuesByCracId.TryGetValue($"{EvmRuntime.RuntimeId}-{op.Batch.Index}", out var cracQueue))
                        {
                            if (cracQueue.TryPeek(out var cracRoot) &&
                                cracRoot is MichelsonOperation)
                                cracQueue.Dequeue(); // skip root operation

                            if (cracQueue.TryPeek(out var cracIdEventOp) &&
                                cracIdEventOp is MichelsonInternalOperation cracIdEvent &&
                                cracIdEvent.Content.RequiredString("kind") == "event" &&
                                cracIdEvent.Content.RequiredString("tag") == "cross_runtime_call" &&
                                cracIdEvent.From == MichelsonRuntime.NullAddress)
                                cracQueue.Dequeue(); // skip crac-id event
                            else
                                throw new Exception("crac-id event missed");

                            while (cracQueue.TryPeek(out var aliasOrigOp) &&
                                aliasOrigOp is MichelsonInternalOperation aliasOrig &&
                                aliasOrig.Content.RequiredString("kind") == "origination" &&
                                aliasOrig.From == MichelsonRuntime.NullAddress)
                                cracQueue.Dequeue(); // skip aliases originations

                            if (cracQueue.TryPeek(out var cracFirst) &&
                                cracFirst is MichelsonInternalOperation micheOp &&
                                micheOp.From == MichelsonRuntime.GetAlias(op.From) &&
                                micheOp.To is string to)
                            {
                                var crac = new CracOperation { GatewayCall = op, TargetCall = micheOp };
                                dest.Operations.Add(crac);
                                queue.Dequeue();

                                if (!MichelsonRuntime.IsCracCall(micheOp.To, micheOp.Content))
                                    cracQueue.Dequeue();

                                ProcessQueue(context, cracQueue, dest, queue, cracParent: crac);
                                break;
                            }
                        }
                    }

                    if (op.From == EvmRuntime.DepositOrigin)
                    {
                        if (context.DelayedOps.FirstOrDefault(x => x.Hash == op.Batch.Hash) is DelayedOperation delayedEop &&
                            delayedEop is DelayedXtzDeposit or DelayedFaDeposit)
                        {
                            var feederCall = op;
                            queue.Dequeue();

                            var bridgeCalls = new List<EvmInternalOperation>(4);

                            while (queue.TryPeek(out var next) && next is EvmInternalOperation bc && bc.Operation == op)
                                bridgeCalls.Add((queue.Dequeue() as EvmInternalOperation)!);

                            dest.Operations.Add(new EvmDeposit { Deposit = delayedEop, FeederCall = feederCall, BridgeCalls = bridgeCalls });
                            break;
                        }
                        // nothing but kernel-synthesized deposit feeders can come from this address
                        throw new Exception("Operation from the deposit origin doesn't match any delayed deposit");
                    }

                    dest.Operations.Add(op);
                    queue.Dequeue();

                    break;
                case EvmInternalOperation op:
                    if (op.Depth < evmDepth) return;
                    if (!evmFrameEntered) evmFrameEntered = true;
                    else if (op.Depth == evmDepth) return;

                    if (EvmRuntime.IsCracCall(op.To, op.Trace))
                    {
                        if (parentQueue is null)
                        {
                            if (context.QueuesByCracId.TryGetValue($"{EvmRuntime.RuntimeId}-{op.Operation.Batch.Index}", out var cracQueue))
                            {
                                if (cracQueue.TryPeek(out var cracRoot) &&
                                    cracRoot is MichelsonOperation)
                                    cracQueue.Dequeue(); // skip root operation

                                if (cracQueue.TryPeek(out var cracIdEventOp) &&
                                    cracIdEventOp is MichelsonInternalOperation cracIdEvent &&
                                    cracIdEvent.Content.RequiredString("kind") == "event" &&
                                    cracIdEvent.Content.RequiredString("tag") == "cross_runtime_call" &&
                                    cracIdEvent.From == MichelsonRuntime.NullAddress)
                                    cracQueue.Dequeue(); // skip crac-id event
                                else
                                    throw new Exception("crac-id event missed");

                                while (cracQueue.TryPeek(out var aliasOrigOp) &&
                                    aliasOrigOp is MichelsonInternalOperation aliasOrig &&
                                    aliasOrig.Content.RequiredString("kind") == "origination" &&
                                    aliasOrig.From == MichelsonRuntime.NullAddress)
                                    cracQueue.Dequeue(); // skip aliases originations

                                if (cracQueue.TryPeek(out var cracFirst) &&
                                    cracFirst is MichelsonInternalOperation micheOp &&
                                    (micheOp.From == MichelsonRuntime.GetAlias(op.From) || EvmRuntime.GetAlias(micheOp.From) == op.From) &&
                                    micheOp.To is string to)
                                {
                                    var crac = new InternalCracOperation { GatewayCall = op, TargetCall = micheOp, CracParent = cracParent };
                                    dest.Operations[^1].Internals.Add(crac);
                                    queue.Dequeue();

                                    if (!MichelsonRuntime.IsCracCall(micheOp.To, micheOp.Content))
                                        cracQueue.Dequeue();

                                    ProcessQueue(context, cracQueue, dest, queue, cracParent: crac);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (parentQueue.TryPeek(out var cracIdEventOp) &&
                                cracIdEventOp is MichelsonInternalOperation cracIdEvent &&
                                cracIdEvent.Content.RequiredString("kind") == "event" &&
                                cracIdEvent.Content.RequiredString("tag") == "cross_runtime_call" &&
                                cracIdEvent.From == MichelsonRuntime.NullAddress)
                                parentQueue.Dequeue(); // skip crac-id event
                            else
                                throw new Exception("crac-id event missed");

                            while (parentQueue.TryPeek(out var aliasOrigOp) &&
                                aliasOrigOp is MichelsonInternalOperation aliasOrig &&
                                aliasOrig.Content.RequiredString("kind") == "origination" &&
                                aliasOrig.From == MichelsonRuntime.NullAddress)
                                parentQueue.Dequeue(); // skip aliases originations

                            if (parentQueue.TryPeek(out var cracFirst) &&
                                cracFirst is MichelsonInternalOperation micheOp &&
                                (micheOp.From == MichelsonRuntime.GetAlias(op.From) || EvmRuntime.GetAlias(micheOp.From) == op.From) &&
                                micheOp.To is string to)
                            {
                                var crac = new InternalCracOperation { GatewayCall = op, TargetCall = micheOp, CracParent = cracParent };
                                dest.Operations[^1].Internals.Add(crac);
                                queue.Dequeue();

                                if (!MichelsonRuntime.IsCracCall(micheOp.To, micheOp.Content))
                                    parentQueue.Dequeue();

                                ProcessQueue(context, parentQueue, dest, cracParent: crac);
                                break;
                            }
                        }
                    }

                    op.CracParent = cracParent;
                    dest.Operations[^1].Internals.Add(op);
                    queue.Dequeue();

                    break;
                case MichelsonOperation op:
                    if (parentQueue != null)
                        throw new InvalidOperationException("should never get here");

                    if (MichelsonRuntime.IsCracCall(op.To, op.Content))
                    {
                        if (context.QueuesByCracId.TryGetValue($"{MichelsonRuntime.RuntimeId}-{op.Batch.Index}", out var cracQueue))
                        {
                            if (cracQueue.TryPeek(out var cracRoot) && cracRoot is EvmOperation)
                                cracQueue.Dequeue(); // skip root operation

                            // TODO: remove this cratch for balance forwarding when previewnet is reset
                            var balanceForwards = new List<(Queue<MetaContent> Queue, int Depth)>();

                            while (cracQueue.TryPeek(out var aliasOrigOp) &&
                                aliasOrigOp is EvmInternalOperation aliasOrig &&
                                aliasOrig.From == EvmRuntime.TezosXCaller)
                            {
                                cracQueue.Dequeue(); // skip aliases originations
                                DequeueBalanceForward(cracQueue, aliasOrig, balanceForwards);
                            }

                            if (cracQueue.TryPeek(out var cracFirst) &&
                                cracFirst is EvmInternalOperation evmOp &&
                                evmOp.From == EvmRuntime.GetAlias(op.From) &&
                                evmOp.To is string to)
                            {
                                var crac = new CracOperation { GatewayCall = op, TargetCall = evmOp };
                                dest.Operations.Add(crac);
                                queue.Dequeue();

                                var frameEntered = !EvmRuntime.IsCracCall(evmOp.To, evmOp.Trace);
                                if (frameEntered)
                                    cracQueue.Dequeue();

                                // TODO: remove this cratch for balance forwarding when previewnet is reset
                                foreach (var (bfQueue, bfDepth) in balanceForwards)
                                    ProcessQueue(context, bfQueue, dest, queue, bfDepth, false, cracParent: crac);

                                ProcessQueue(context, cracQueue, dest, queue, evmOp.Depth, frameEntered, cracParent: crac);
                                break;
                            }
                        }
                    }

                    if (op.From == MichelsonRuntime.DepositOrigin)
                    {
                        if (context.DelayedOps.FirstOrDefault(x => x.Hash == op.Batch.Hash) is DelayedOperation delayedMop &&
                            delayedMop is DelayedXtzDeposit or DelayedFaDeposit)
                        {
                            var feederCall = op;
                            queue.Dequeue();

                            var bridgeCalls = new List<MichelsonInternalOperation>(4);
                            while (queue.TryPeek(out var next) && next is MichelsonInternalOperation bc && bc.Operation == op)
                                bridgeCalls.Add((queue.Dequeue() as MichelsonInternalOperation)!);

                            dest.Operations.Add(new MichelsonDeposit { Deposit = delayedMop, FeederCall = feederCall, BridgeCalls = bridgeCalls });
                            break;
                        }
                        // nothing but kernel-synthesized deposit feeders can come from this address
                        throw new Exception("Operation from the deposit origin doesn't match any delayed deposit");
                    }

                    dest.Operations.Add(op);
                    queue.Dequeue();

                    break;
                case MichelsonInternalOperation op:
                    if (op.From == MichelsonRuntime.CracOrigin && op.Content.RequiredString("kind") == "event")
                    {
                        if (op.Content.RequiredString("tag") != "cross_runtime_call_end")
                            throw new Exception("Unexpected crac event");

                        queue.Dequeue();
                        return;
                    }

                    if (MichelsonRuntime.IsCracCall(op.To, op.Content))
                    {
                        if (parentQueue is null)
                        {
                            if (context.QueuesByCracId.TryGetValue($"{MichelsonRuntime.RuntimeId}-{op.Operation.Batch.Index}", out var cracQueue))
                            {
                                if (cracQueue.TryPeek(out var cracRoot) && cracRoot is EvmOperation)
                                    cracQueue.Dequeue(); // skip root operation

                                // TODO: remove this cratch for balance forwarding when previewnet is reset
                                var balanceForwards = new List<(Queue<MetaContent> Queue, int Depth)>();

                                while (cracQueue.TryPeek(out var aliasOrigOp) &&
                                    aliasOrigOp is EvmInternalOperation aliasOrig &&
                                    aliasOrig.From == EvmRuntime.TezosXCaller)
                                {
                                    cracQueue.Dequeue(); // skip aliases originations
                                    DequeueBalanceForward(cracQueue, aliasOrig, balanceForwards);
                                }

                                if (cracQueue.TryPeek(out var cracFirst) &&
                                    cracFirst is EvmInternalOperation evmOp &&
                                    (evmOp.From == EvmRuntime.GetAlias(op.From) || MichelsonRuntime.GetAlias(evmOp.From) == op.From) &&
                                    evmOp.To is string to)
                                {
                                    var crac = new InternalCracOperation { GatewayCall = op, TargetCall = evmOp, CracParent = cracParent };
                                    dest.Operations[^1].Internals.Add(crac);
                                    queue.Dequeue();

                                    var frameEntered = !EvmRuntime.IsCracCall(evmOp.To, evmOp.Trace);
                                    if (frameEntered)
                                        cracQueue.Dequeue();

                                    // TODO: remove this cratch for balance forwarding when previewnet is reset
                                    foreach (var (bfQueue, bfDepth) in balanceForwards)
                                        ProcessQueue(context, bfQueue, dest, queue, bfDepth, false, cracParent: crac);

                                    ProcessQueue(context, cracQueue, dest, queue, evmOp.Depth, frameEntered, cracParent: crac);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // TODO: remove this cratch for balance forwarding when previewnet is reset
                            var balanceForwards = new List<(Queue<MetaContent> Queue, int Depth)>();

                            while (parentQueue.TryPeek(out var aliasOrigOp) &&
                                aliasOrigOp is EvmInternalOperation aliasOrig &&
                                aliasOrig.From == EvmRuntime.TezosXCaller)
                            {
                                parentQueue.Dequeue(); // skip aliases originations
                                DequeueBalanceForward(parentQueue, aliasOrig, balanceForwards);
                            }

                            if (parentQueue.TryPeek(out var cracFirst) &&
                                cracFirst is EvmInternalOperation evmOp &&
                                (evmOp.From == EvmRuntime.GetAlias(op.From) || MichelsonRuntime.GetAlias(evmOp.From) == op.From) &&
                                evmOp.To is string to)
                            {
                                var crac = new InternalCracOperation { GatewayCall = op, TargetCall = evmOp, CracParent = cracParent };
                                dest.Operations[^1].Internals.Add(crac);
                                queue.Dequeue();

                                var frameEntered = !EvmRuntime.IsCracCall(evmOp.To, evmOp.Trace);
                                if (frameEntered)
                                    parentQueue.Dequeue();

                                // TODO: remove this cratch for balance forwarding when previewnet is reset
                                foreach (var (bfQueue, bfDepth) in balanceForwards)
                                    ProcessQueue(context, bfQueue, dest, null, bfDepth, false, cracParent: crac);

                                ProcessQueue(context, parentQueue, dest, null, evmOp.Depth, frameEntered, cracParent: crac);
                                break;
                            }
                        }
                    }

                    op.CracParent = cracParent;
                    dest.Operations[^1].Internals.Add(op);
                    queue.Dequeue();

                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    // TODO: remove this cratch for balance forwarding when previewnet is reset
    static void DequeueBalanceForward(Queue<MetaContent> queue, EvmInternalOperation aliasOrig, List<(Queue<MetaContent> Queue, int Depth)> dest)
    {
        if (!queue.TryPeek(out var bfOp) || bfOp is not EvmInternalOperation bf || bf.Depth <= aliasOrig.Depth)
            return;

        var forwardQueue = new Queue<MetaContent>();
        while (queue.TryPeek(out var next) && next is EvmInternalOperation op && op.Depth > aliasOrig.Depth)
            forwardQueue.Enqueue(queue.Dequeue());

        dest.Add((forwardQueue, bf.Depth));
    }
}
