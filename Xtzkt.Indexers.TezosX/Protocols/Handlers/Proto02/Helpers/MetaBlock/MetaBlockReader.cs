using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers.MetaBlock;

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
                    var delayedDeposit = Delayed.FirstOrDefault(x => x.Hash == op.Batch.Hash);
                    if ((delayedDeposit is DelayedDeposit or DelayedFaDeposit) != (op.From == EvmRuntime.NullAddress))
                        throw new Exception($"Operation {op.Batch.Hash} is a delayed deposit, but wasn't sent by the kernel, or vice versa");

                    if (delayedDeposit is DelayedDeposit xtzDeposit)
                    {
                        if (op.Tx.RequiredHexBigInteger("value") != xtzDeposit.Amount ||
                            op.Tx.RequiredString("to") != xtzDeposit.Receiver)
                            throw new Exception($"Delayed xtz deposit {op.Batch.Hash} doesn't match its pseudo transaction");

                        dest.Operations.Add(new MetaOperation(new DelayedEvmDepositOperation(delayedDeposit, op)));
                        queue.Dequeue();
                        break;
                    }

                    if (delayedDeposit is DelayedFaDeposit faDeposit)
                    {
                        if (op.Tx.RequiredHexBigInteger("value") != 0 ||
                            op.Tx.RequiredString("to") != (faDeposit.Proxy ?? faDeposit.Receiver))
                            throw new Exception($"Delayed fa deposit {op.Batch.Hash} doesn't match its pseudo transaction");

                        var operation = new MetaOperation(new DelayedEvmDepositOperation(delayedDeposit, op));

                        if (faDeposit.Proxy != null)
                            operation.Internals.Add(new MetaInternalOperation(SynthesizeProxyCall(op, faDeposit)));

                        dest.Operations.Add(operation);
                        queue.Dequeue();
                        break;
                    }

                    dest.Operations.Add(new MetaOperation(op));
                    queue.Dequeue();
                    break;
                case EvmInternalOperation op:
                    dest.Operations[^1].Internals.Add(new MetaInternalOperation(op));
                    queue.Dequeue();
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    static readonly byte[] FaProxyDepositSelector = [0x0e, 0xfe, 0x6a, 0x8b]; // deposit(address,uint256,uint256)
    static readonly string FaDepositTopic = Hex.Convert(FaBridgeEvents.DepositTopic);

    // The bridge->proxy call is missing from the trace, while its subcalls are present as the root's children. We try to restore it.
    EvmInternalOperation SynthesizeProxyCall(EvmOperation feederCall, DelayedFaDeposit faDeposit)
    {
        var succeeded = true;
        foreach (var log in feederCall.Logs)
        {
            if (log.RequiredString("address") != EvmRuntime.NullAddress)
                continue;

            var topics = log.RequiredArray("topics");
            if (topics.GetArrayLength() == 2 && topics[0].RequiredString() == FaDepositTopic)
            {
                succeeded = Hex.Convert(log.RequiredHexBytes("data").AsSpan(12, 20)) == faDeposit.Proxy;
                break;
            }
        }

        // a failed proxy call is not rolled back in this era, so its surviving logs must stay on the deposit operation
        var logs = new List<JsonElement>();
        if (succeeded)
        {
            logs.AddRange(feederCall.Logs.Where(x => x.RequiredString("address") != EvmRuntime.NullAddress));
            feederCall.Logs.RemoveAll(x => x.RequiredString("address") != EvmRuntime.NullAddress);
        }

        var input = new byte[4 + 32 * 3];
        FaProxyDepositSelector.CopyTo(input, 0);
        var receiver = Hex.Parse(faDeposit.Receiver);
        receiver.CopyTo(input, 4 + 32 - receiver.Length);
        var amount = faDeposit.Amount.ToByteArray(isUnsigned: true, isBigEndian: true);
        amount.CopyTo(input, 4 + 64 - amount.Length);
        faDeposit.TicketHash.CopyTo(input, 4 + 96 - faDeposit.TicketHash.Length);

        var trace = JsonSerializer.SerializeToElement(new
        {
            type = "CALL",
            from = EvmRuntime.NullAddress,
            to = faDeposit.Proxy,
            value = "0x0",
            gas = "0xf4240", // FA_DEPOSIT_PROXY_GAS_LIMIT
            gasUsed = "0x0",
            input = Hex.Convert(input),
        });

        return new EvmInternalOperation(feederCall, trace, 1,
            succeeded ? OperationStatus.Applied : OperationStatus.Failed,
            OperationStatus.Applied,
            logs);
    }
}
