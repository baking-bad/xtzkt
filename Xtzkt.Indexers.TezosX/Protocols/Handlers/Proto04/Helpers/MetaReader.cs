using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto04.Helpers;

public partial class ProtoHelpers
{
    protected override MetaBatch? TryReadOperation(MetaContext context, string hash, bool delayed)
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

    protected void ProcessQueue(MetaContext context, Queue<MetaContent> queue, MetaBatch dest)
    {
        while (queue.TryPeek(out var candidate))
        {
            switch (candidate)
            {
                case EvmOperation op:
                    var delayedDeposit = context.DelayedOps.FirstOrDefault(x => x.Hash == op.Batch.Hash);
                    if ((delayedDeposit is DelayedXtzDeposit or DelayedFaDeposit) != (op.From == EvmRuntime.NullAddress))
                        throw new Exception($"Operation {op.Batch.Hash} is a delayed deposit, but wasn't sent by the kernel, or vice versa");

                    if (delayedDeposit is DelayedXtzDeposit xtzDeposit)
                    {
                        if (op.Tx.RequiredHexBigInteger("value") != xtzDeposit.Amount ||
                            op.Tx.RequiredString("to") != xtzDeposit.Receiver)
                            throw new Exception($"Delayed xtz deposit {op.Batch.Hash} doesn't match its pseudo transaction");

                        dest.Operations.Add(new EvmDeposit { Deposit = delayedDeposit, FeederCall = op });
                        queue.Dequeue();
                        break;
                    }

                    if (delayedDeposit is DelayedFaDeposit faDeposit)
                    {
                        if (op.Tx.RequiredHexBigInteger("value") != 0 ||
                            op.Tx.RequiredString("to") != (faDeposit.Proxy ?? faDeposit.Receiver))
                            throw new Exception($"Delayed fa deposit {op.Batch.Hash} doesn't match its pseudo transaction");

                        dest.Operations.Add(new EvmDeposit { Deposit = delayedDeposit, FeederCall = op });
                        queue.Dequeue();
                        break;
                    }

                    dest.Operations.Add(op);
                    queue.Dequeue();
                    break;
                case EvmInternalOperation op:
                    dest.Operations[^1].Internals.Add(op);
                    queue.Dequeue();
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
