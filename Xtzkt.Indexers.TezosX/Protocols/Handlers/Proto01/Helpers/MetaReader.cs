using System.Text.Json;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;

partial class ProtoHelpers
{
    protected MetaBatch? TryReadOperation(MetaContext context, string hash, bool delayed)
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
                    if (delayedDeposit is DelayedXtzDeposit or DelayedFaDeposit && op.From != ExpectedDepositSender(delayedDeposit))
                        throw new Exception($"Delayed deposit {op.Batch.Hash} wasn't sent by the kernel");

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
                            op.Tx.RequiredString("to") != ExpectedFaDepositTarget(faDeposit))
                            throw new Exception($"Delayed fa deposit {op.Batch.Hash} doesn't match its pseudo transaction");

                        var faDepositOp = CreateFaDeposit(op, faDeposit);
                        dest.Operations.Add(faDepositOp);
                        queue.Dequeue();
                        DrainBridgeCalls(queue, faDepositOp);
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

    protected virtual string ExpectedDepositSender(DelayedOperation deposit)
    {
        return EvmRuntime.NullAddress;
    }

    protected virtual string ExpectedFaDepositTarget(DelayedFaDeposit faDeposit)
    {
        return faDeposit.Proxy ?? faDeposit.Receiver;
    }

    protected virtual void DrainBridgeCalls(Queue<MetaContent> queue, EvmDeposit deposit)
    {
    }

    protected virtual EvmDeposit CreateFaDeposit(EvmOperation feederCall, DelayedFaDeposit faDeposit)
    {
        var operation = new EvmDeposit { Deposit = faDeposit, FeederCall = feederCall };

        if (faDeposit.Proxy != null)
            // The bridge->proxy call is missing from the trace, while its subcalls are present as the root's children. We try to restore it.
            operation.Internals.Add(SynthesizeProxyCall(feederCall, faDeposit));

        return operation;
    }

    static readonly byte[] FaProxyDepositSelector = [0x0e, 0xfe, 0x6a, 0x8b]; // deposit(address,uint256,uint256)

    static readonly string FaDepositTopic = Hex.GetString(FaBridgeEvents.DepositTopic);

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
                succeeded = Hex.GetString(log.RequiredHexBytes("data").AsSpan(12, 20)) == faDeposit.Proxy;
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
        var receiver = Hex.GetBytes(faDeposit.Receiver);
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
            input = Hex.GetString(input),
        });

        return new EvmInternalOperation
        {
            Operation = feederCall,
            Trace = trace,
            Depth = 1,
            Status = succeeded ? OperationStatus.Applied : OperationStatus.Failed,
            ParentStatus = OperationStatus.Applied,
            From = EvmRuntime.NullAddress,
            To = faDeposit.Proxy,
            Logs = logs,
        };
    }
}
