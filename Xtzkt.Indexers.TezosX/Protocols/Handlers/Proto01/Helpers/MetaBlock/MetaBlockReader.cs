using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

public class MetaBlockReader(IEvmRuntime evmRuntime, List<IDelayedTransaction> delayed, Dictionary<string, Queue<IMetaContent>> ops)
{
    readonly IEvmRuntime EvmRuntime = evmRuntime;
    readonly List<IDelayedTransaction> Delayed = delayed;
    readonly Dictionary<string, Queue<IMetaContent>> Operations = ops;

    public MetaBatch? TryReadOperation(string hash, bool delayed)
    {
        // operation from blueprint can be dropped and not appear in the block at all
        if (!Operations.TryGetValue(hash, out var queue))
            return null;

        var batch = new MetaBatch(hash, delayed);
        ProcessQueue(queue, batch);

        return batch;
    }

    void ProcessQueue(Queue<IMetaContent> queue, MetaBatch dest)
    {
        while (queue.TryPeek(out var candidate))
        {
            switch (candidate)
            {
                case EvmOperation op:
                    var delayedDeposit = Delayed.FirstOrDefault(x => x.Hash == op.Batch.Hash) as DelayedDeposit;
                    if (delayedDeposit != null != (op.From == EvmRuntime.NullAddress))
                        throw new Exception($"Operation {op.Batch.Hash} is a delayed deposit, but wasn't sent by the kernel, or vice versa");

                    if (delayedDeposit != null)
                    {
                        if (delayedDeposit.Amount != op.Tx.RequiredHexBigInteger("value") ||
                            delayedDeposit.Receiver != op.Tx.RequiredString("to"))
                            throw new Exception($"Delayed deposit {op.Batch.Hash} doesn't match its pseudo transaction");

                        dest.Operations.Add(new MetaOperation(new DelayedEvmDepositOperation(delayedDeposit, op)));
                        queue.Dequeue();
                        break;
                    }

                    dest.Operations.Add(new MetaOperation(op));
                    queue.Dequeue();
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
