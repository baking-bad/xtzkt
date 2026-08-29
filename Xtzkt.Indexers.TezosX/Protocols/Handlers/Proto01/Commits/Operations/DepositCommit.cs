using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class DepositCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public async Task<XEvmDepositOperation> ApplyEvm(string hash, DelayedOperation deposit, JsonElement feederReceipt)
    {
        #region init
        var block = Context.Block;

        var (type, amount, receiverAddress, inboxLevel, inboxMessageId, proxyAddress, ticketHash) = deposit is DelayedXtzDeposit xtzDeposit
            ? (DepositType.Xtz, xtzDeposit.Amount, xtzDeposit.Receiver, xtzDeposit.InboxLevel, xtzDeposit.InboxMessageId, null, null)
            : deposit is DelayedFaDeposit faDeposit
                ? (DepositType.Fa, faDeposit.Amount, faDeposit.Receiver, faDeposit.InboxLevel, faDeposit.InboxMessageId, faDeposit.Proxy, faDeposit.TicketHash)
                : throw new InvalidOperationException("Invalid deposit type");

        var receiver = await Helpers.GetOrCreateXEvmAddress(receiverAddress);
        var proxy = proxyAddress == null ? null : await Helpers.GetOrCreateXEvmAddress(proxyAddress);

        var status = feederReceipt.RequiredEvmOpStatus("status");

        var op = new XEvmDepositOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            Status = status,
            Amount = amount,
            ReceiverId = receiver.Id,
            InboxLevel = inboxLevel,
            InboxMessageId = inboxMessageId,
            Type = type,
            TicketHash = ticketHash,
            ProxyId = proxy?.Id,
            DepositId = GetDepositId(feederReceipt),
            GasUsed = feederReceipt.RequiredHexInt32("gasUsed"),

            #region crutch for nested proxy calls in old etherlink
            SenderId = (await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress)).Id,
            Counter = 0,
            InternalOperations = null,
            #endregion
        };
        #endregion

        #region apply operation
        Db.TryAttach(receiver);
        receiver.DepositOpsCount++;
        receiver.LastLevel = op.Level;
        receiver.LastTimestamp = op.Timestamp;

        if (proxy != null && proxy != receiver)
        {
            Db.TryAttach(proxy);
            proxy.DepositOpsCount++;
            proxy.LastLevel = op.Level;
            proxy.LastTimestamp = op.Timestamp;
        }

        Context.Block.Operations |= XOperations.Deposit;

        Cache.Chain.Get().DepositOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            if (op.TicketHash == null)
            {
                Receive(receiver, op.Amount);
                Context.Statistics.TotalCreated += op.Amount;
            }
        }
        #endregion

        Db.DepositOps.Add(op);
        Context.DepositOps.Add(op);

        return op;
    }

    protected virtual BigInteger? GetDepositId(JsonElement feederReceipt)
    {
        // deposit queue starts from Dionysus
        return null;
    }

    public async Task Revert(XEvmDepositOperation op)
    {
        #region init
        var receiver = (await Cache.Addresses.GetAsync(op.ReceiverId) as XEvmAddress)!;
        var proxy = await Cache.Addresses.GetAsync(op.ProxyId) as XEvmAddress;

        Db.TryAttach(receiver);
        Db.TryAttach(proxy);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            if (op.TicketHash == null)
            {
                RevertReceive(receiver, op.Amount);
            }
        }
        #endregion

        #region revert operation
        receiver.DepositOpsCount--;
        receiver.LastLevel = op.Level;
        receiver.LastTimestamp = op.Timestamp;
        if (receiver.IsEmpty()) await Helpers.RemoveXEvmAddress(receiver);

        if (proxy != null && proxy != receiver)
        {
            proxy.DepositOpsCount--;
            proxy.LastLevel = op.Level;
            proxy.LastTimestamp = op.Timestamp;
            if (proxy.IsEmpty()) await Helpers.RemoveXEvmAddress(proxy);
        }

        Cache.Chain.Get().DepositOpsCount--;
        #endregion

        Db.DepositOps.Remove(op);
        Cache.Chain.ReleaseOperationId();
    }
}
