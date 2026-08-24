using System.Numerics;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08
{
    class DepositCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        const string XtzQueuedDepositTopic = "0x1db8461f75e6c8b8303be39f8f9e8641e37968c840ff0f8e465cf3c9b18d9d7d";
        const string FaQueuedDepositTopic = "0xb02d79c5657e344e23d91529b954c3087c60a974d598939583904a4f0b959614";

        public async Task<XEvmDepositOperation> ApplyEvm(string hash, IDelayedTransaction deposit, JsonElement feederReceipt)
        {
            #region init
            var block = Context.Block;

            var (type, amount, receiverAddress, inboxLevel, inboxMessageId, proxyAddress, ticketHash) = deposit is DelayedDeposit xtzDeposit
                ? (DepositType.Xtz, xtzDeposit.Amount, xtzDeposit.Receiver, xtzDeposit.InboxLevel, xtzDeposit.InboxMessageId, null, null)
                : deposit is DelayedFaDeposit faDeposit
                    ? (DepositType.Fa, faDeposit.Amount, faDeposit.Receiver, faDeposit.InboxLevel, faDeposit.InboxMessageId, faDeposit.Proxy, faDeposit.TicketHash)
                    : throw new InvalidOperationException("Invalid deposit type");

            var receiver = await GetOrCreateXEvmAddress(receiverAddress);
            var proxy = proxyAddress == null ? null : await GetOrCreateXEvmAddress(proxyAddress);

            var status = feederReceipt.RequiredEvmOpStatus("status");

            BigInteger? depositId = null;
            foreach (var log in feederReceipt.RequiredArray("logs").EnumerateArray())
            {
                var topic = log.RequiredArray("topics")[0].RequiredString();
                if (topic == XtzQueuedDepositTopic)
                {
                    depositId = new BigInteger(log.Required("data").RequiredHexBytes().AsSpan(32, 32), true, true);
                    break;
                }
                if (topic == FaQueuedDepositTopic)
                {
                    depositId = new BigInteger(log.Required("data").RequiredHexBytes().AsSpan(0, 32), true, true);
                    break;
                }
            }

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
                DepositId = depositId
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
                if (op.DepositId == null)
                {
                    if (op.TicketHash == null)
                    {
                        Receive(receiver, op.Amount);
                        Context.Statistics.TotalCreated += op.Amount;
                    }
                }
                else
                {
                    if (op.TicketHash == null)
                    {
                        var bridge = (await Cache.Addresses.GetExistingAsync(EvmRuntime.XtzBridge) as XEvmAddress)!;
                        Db.TryAttach(bridge);
                        Receive(bridge, op.Amount);
                        bridge.LastLevel = op.Level;
                        bridge.LastTimestamp = op.Timestamp;
                        Context.Statistics.TotalCreated += op.Amount;
                    }
                }
            }
            #endregion

            Db.DepositOps.Add(op);
            Context.DepositOps.Add(op);

            return op;
        }

        public async Task ApplyMichelson(string hash, IDelayedTransaction deposit, JsonElement feederContent)
        {
            #region init
            var block = Context.Block;

            var (amount, receiverAddress, inboxLevel, inboxMessageId) = deposit is DelayedDeposit xtzDeposit
                ? (xtzDeposit.Amount, xtzDeposit.Receiver, xtzDeposit.InboxLevel, xtzDeposit.InboxMessageId)
                : deposit is DelayedFaDeposit
                    ? throw new NotImplementedException("FA deposits are not supported by the Michelson runtime")
                    : throw new InvalidOperationException("Invalid deposit type");

            var receiver = await GetOrCreateXMichelsonAddress(receiverAddress);

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
                if (op.DepositId == null)
                {
                    if (op.TicketHash == null)
                    {
                        RevertReceive(receiver, op.Amount);
                    }
                }
                else
                {
                    if (op.TicketHash == null)
                    {
                        var bridge = (await Cache.Addresses.GetExistingAsync(EvmRuntime.XtzBridge) as XEvmAddress)!;
                        Db.TryAttach(bridge);
                        RevertReceive(bridge, op.Amount);
                        bridge.LastLevel = op.Level;
                        bridge.LastTimestamp = op.Timestamp;
                    }
                }
            }
            #endregion

            #region revert operation
            receiver.DepositOpsCount--;
            receiver.LastLevel = op.Level;
            receiver.LastTimestamp = op.Timestamp;
            if (receiver.IsEmpty()) await RemoveXEvmAddress(receiver);

            if (proxy != null && proxy != receiver)
            {
                proxy.DepositOpsCount--;
                proxy.LastLevel = op.Level;
                proxy.LastTimestamp = op.Timestamp;
                if (proxy.IsEmpty()) await RemoveXEvmAddress(proxy);
            }

            Cache.Chain.Get().DepositOpsCount--;
            #endregion

            Db.DepositOps.Remove(op);
            Cache.Chain.ReleaseOperationId();
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
            if (receiver.IsEmpty()) await RemoveXMichelsonAddress(receiver);

            Cache.Chain.Get().DepositOpsCount--;
            #endregion

            Db.DepositOps.Remove(op);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
