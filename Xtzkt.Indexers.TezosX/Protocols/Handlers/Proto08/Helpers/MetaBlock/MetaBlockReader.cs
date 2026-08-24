using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

public class MetaBlockReader(List<IDelayedTransaction> delayed, Dictionary<string, Queue<IMetaContent>> ops, Dictionary<string, Queue<IMetaContent>> cracs)
{
    readonly List<IDelayedTransaction> Delayed = delayed;
    readonly Dictionary<string, Queue<IMetaContent>> Operations = ops;
    readonly Dictionary<string, Queue<IMetaContent>> Cracs = cracs;

    public MetaBatch? TryReadOperation(string hash, bool delayed)
    {
        // operation from blueprint can be dropped and not appear in the block at all
        if (!Operations.TryGetValue(hash, out var queue))
            return null;

        var batch = new MetaBatch(hash, delayed);
        ProcessQueue(queue, batch);

        return batch;
    }

    void ProcessQueue(Queue<IMetaContent> queue, MetaBatch dest, Queue<IMetaContent>? parentQueue = null, int evmDepth = 0, bool evmFrameEntered = false, IMetaContent? cracParent = null)
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
                        if (Cracs.TryGetValue($"{EvmRuntime.RuntimeId}-{op.Batch.Index}", out var cracQueue))
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
                                var crac = new CracOperation(op, micheOp);
                                dest.Operations.Add(new MetaOperation(crac));
                                queue.Dequeue();

                                if (!MichelsonRuntime.IsCracCall(micheOp.To, micheOp.Content))
                                    cracQueue.Dequeue();

                                ProcessQueue(cracQueue, dest, queue, cracParent: crac);
                                break;
                            }
                        }
                    }

                    if (op.From == EvmRuntime.DepositOrigin)
                    {
                        if (Delayed.FirstOrDefault(x => x.Hash == op.Batch.Hash) is IDelayedTransaction delayedEop &&
                            delayedEop is DelayedDeposit or DelayedFaDeposit)
                        {
                            var feederCall = op;
                            queue.Dequeue();

                            var bridgeCalls = new List<EvmInternalOperation>(4);
                            foreach (var _ in op.Internals)
                                bridgeCalls.Add((queue.Dequeue() as EvmInternalOperation)!);

                            dest.Operations.Add(new MetaOperation(new DelayedEvmDepositOperation(delayedEop, feederCall, bridgeCalls)));
                            break;
                        }
                    }

                    dest.Operations.Add(new MetaOperation(op));
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
                            if (Cracs.TryGetValue($"{EvmRuntime.RuntimeId}-{op.Operation.Batch.Index}", out var cracQueue))
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
                                    var crac = new InternalCracOperation(op, micheOp);
                                    dest.Operations[^1].Internals.Add(new MetaInternalOperation(crac, cracParent));
                                    queue.Dequeue();

                                    if (!MichelsonRuntime.IsCracCall(micheOp.To, micheOp.Content))
                                        cracQueue.Dequeue();

                                    ProcessQueue(cracQueue, dest, queue, cracParent: crac);
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
                                var crac = new InternalCracOperation(op, micheOp);
                                dest.Operations[^1].Internals.Add(new MetaInternalOperation(crac, cracParent));
                                queue.Dequeue();

                                if (!MichelsonRuntime.IsCracCall(micheOp.To, micheOp.Content))
                                    parentQueue.Dequeue();

                                ProcessQueue(parentQueue, dest, cracParent: crac);
                                break;
                            }
                        }
                    }

                    dest.Operations[^1].Internals.Add(new MetaInternalOperation(op, cracParent));
                    queue.Dequeue();

                    break;
                case MichelsonOperation op:
                    if (parentQueue != null)
                        throw new InvalidOperationException("should never get here");

                    if (MichelsonRuntime.IsCracCall(op.To, op.Content))
                    {
                        if (Cracs.TryGetValue($"{MichelsonRuntime.RuntimeId}-{op.Batch.Index}", out var cracQueue))
                        {
                            if (cracQueue.TryPeek(out var cracRoot) && cracRoot is EvmOperation)
                                cracQueue.Dequeue(); // skip root operation

                            // TODO: remove this cratch for balance forwarding when previewnet is reset
                            var balanceForwards = new List<(Queue<IMetaContent> Queue, int Depth)>();

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
                                var crac = new CracOperation(op, evmOp);
                                dest.Operations.Add(new MetaOperation(crac));
                                queue.Dequeue();

                                var frameEntered = !EvmRuntime.IsCracCall(evmOp.To, evmOp.Trace);
                                if (frameEntered)
                                    cracQueue.Dequeue();

                                // TODO: remove this cratch for balance forwarding when previewnet is reset
                                foreach (var (bfQueue, bfDepth) in balanceForwards)
                                    ProcessQueue(bfQueue, dest, queue, bfDepth, false, cracParent: crac);

                                ProcessQueue(cracQueue, dest, queue, evmOp.Depth, frameEntered, cracParent: crac);
                                break;
                            }
                        }
                    }
                    
                    if (op.From == MichelsonRuntime.DepositOrigin)
                    {
                        if (Delayed.FirstOrDefault(x => x.Hash == op.Batch.Hash) is IDelayedTransaction delayedMop &&
                            delayedMop is DelayedDeposit or DelayedFaDeposit)
                        {
                            var feederCall = op;
                            queue.Dequeue();

                            var bridgeCalls = new List<MichelsonInternalOperation>(4);
                            foreach (var _ in op.Internals)
                                bridgeCalls.Add((queue.Dequeue() as MichelsonInternalOperation)!);

                            dest.Operations.Add(new MetaOperation(new DelayedMichelsonDepositOperation(delayedMop, feederCall, bridgeCalls)));
                            break;
                        }
                    }

                    dest.Operations.Add(new MetaOperation(op));
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
                            if (Cracs.TryGetValue($"{MichelsonRuntime.RuntimeId}-{op.Operation.Batch.Index}", out var cracQueue))
                            {
                                if (cracQueue.TryPeek(out var cracRoot) && cracRoot is EvmOperation)
                                    cracQueue.Dequeue(); // skip root operation

                                // TODO: remove this cratch for balance forwarding when previewnet is reset
                                var balanceForwards = new List<(Queue<IMetaContent> Queue, int Depth)>();

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
                                    var crac = new InternalCracOperation(op, evmOp);
                                    dest.Operations[^1].Internals.Add(new MetaInternalOperation(crac, cracParent));
                                    queue.Dequeue();

                                    var frameEntered = !EvmRuntime.IsCracCall(evmOp.To, evmOp.Trace);
                                    if (frameEntered)
                                        cracQueue.Dequeue();

                                    // TODO: remove this cratch for balance forwarding when previewnet is reset
                                    foreach (var (bfQueue, bfDepth) in balanceForwards)
                                        ProcessQueue(bfQueue, dest, queue, bfDepth, false, cracParent: crac);

                                    ProcessQueue(cracQueue, dest, queue, evmOp.Depth, frameEntered, cracParent: crac);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // TODO: remove this cratch for balance forwarding when previewnet is reset
                            var balanceForwards = new List<(Queue<IMetaContent> Queue, int Depth)>();

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
                                var crac = new InternalCracOperation(op, evmOp);
                                dest.Operations[^1].Internals.Add(new MetaInternalOperation(crac, cracParent));
                                queue.Dequeue();

                                var frameEntered = !EvmRuntime.IsCracCall(evmOp.To, evmOp.Trace);
                                if (frameEntered)
                                    parentQueue.Dequeue();

                                // TODO: remove this cratch for balance forwarding when previewnet is reset
                                foreach (var (bfQueue, bfDepth) in balanceForwards)
                                    ProcessQueue(bfQueue, dest, null, bfDepth, false, cracParent: crac);

                                ProcessQueue(parentQueue, dest, null, evmOp.Depth, frameEntered, cracParent: crac);
                                break;
                            }
                        }
                    }

                    dest.Operations[^1].Internals.Add(new MetaInternalOperation(op, cracParent));
                    queue.Dequeue();

                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    // TODO: remove this cratch for balance forwarding when previewnet is reset
    static void DequeueBalanceForward(Queue<IMetaContent> queue, EvmInternalOperation aliasOrig, List<(Queue<IMetaContent> Queue, int Depth)> dest)
    {
        if (!queue.TryPeek(out var bfOp) || bfOp is not EvmInternalOperation bf || bf.Depth <= aliasOrig.Depth)
            return;

        var forwardQueue = new Queue<IMetaContent>();
        while (queue.TryPeek(out var next) && next is EvmInternalOperation op && op.Depth > aliasOrig.Depth)
            forwardQueue.Enqueue(queue.Dequeue());

        dest.Add((forwardQueue, bf.Depth));
    }
}
