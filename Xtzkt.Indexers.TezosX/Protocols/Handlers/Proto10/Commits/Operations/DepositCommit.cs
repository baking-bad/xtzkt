using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class DepositCommit(ProtocolHandler protocol) : Proto01.DepositCommit(protocol)
{
    protected override BigInteger? GetDepositId(JsonElement feederReceipt)
    {
        foreach (var log in feederReceipt.RequiredArray("logs").EnumerateArray())
        {
            var topics = log.RequiredArray("topics");
            if (topics.EnumerateArray().Any())
            {
                // XtzQueuedDepositTopic
                if (topics[0].ValueEquals("0x1db8461f75e6c8b8303be39f8f9e8641e37968c840ff0f8e465cf3c9b18d9d7d"))
                    return new BigInteger(log.Required("data").RequiredHexBytes().AsSpan(32, 32), true, true);

                // FaQueuedDepositTopic
                if (topics[0].ValueEquals("0xb02d79c5657e344e23d91529b954c3087c60a974d598939583904a4f0b959614"))
                    return new BigInteger(log.Required("data").RequiredHexBytes().AsSpan(0, 32), true, true);
            }
        }
        return null;
    }

    public async Task ApplyMichelson(byte[] hash, DelayedOperation deposit, JsonElement feederContent)
    {
        #region init
        var block = Context.Block;

        var (amount, receiverAddress, inboxLevel, inboxMessageId) = deposit is DelayedXtzDeposit xtzDeposit
            ? (xtzDeposit.Amount, xtzDeposit.Receiver, xtzDeposit.InboxLevel, xtzDeposit.InboxMessageId)
            : deposit is DelayedFaDeposit
                ? throw new NotImplementedException("FA deposits are not supported by the Michelson runtime")
                : throw new InvalidOperationException("Invalid deposit type");

        var receiver = await Helpers.GetOrCreateXMichelsonAddress(receiverAddress);

        var metadata = feederContent.Required("metadata");
        var result = metadata.Required("operation_result");
        var status = result.RequiredOpStatus("status");

        var op = new XMichelsonDepositOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            Status = status,
            Amount = (long)(amount / M12), // TODO: make sure rounding loss is impossible
            ReceiverId = receiver.Id,
            InboxLevel = inboxLevel,
            InboxMessageId = inboxMessageId,
            Type = DepositType.Xtz,
            // TODO: extract deposit id when they implement queue
        };
        #endregion

        #region apply operation
        Db.TryAttach(receiver);
        receiver.DepositOpsCount++;
        receiver.LastLevel = op.Level;
        receiver.LastTimestamp = op.Timestamp;

        Context.Block.Operations |= XOperations.Deposit;

        Cache.Chain.Get().DepositOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            Receive(receiver, op.Amount);
            Context.Statistics.TotalCreated += new BigInteger(op.Amount) * M12;
        }
        #endregion

        Db.DepositOps.Add(op);
        Context.DepositOps.Add(op);
    }

    public async Task Revert(XMichelsonDepositOperation op)
    {
        #region init
        var receiver = (await Cache.Addresses.GetAsync(op.ReceiverId) as XMichelsonAddress)!;

        Db.TryAttach(receiver);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            RevertReceive(receiver, op.Amount);
        }
        #endregion

        #region revert operation
        receiver.DepositOpsCount--;
        receiver.LastLevel = op.Level;
        receiver.LastTimestamp = op.Timestamp;
        if (receiver.IsEmpty()) await Helpers.RemoveXMichelsonAddress(receiver);

        Cache.Chain.Get().DepositOpsCount--;
        #endregion

        Db.DepositOps.Remove(op);
        Cache.Chain.ReleaseOperationId();
    }
}
