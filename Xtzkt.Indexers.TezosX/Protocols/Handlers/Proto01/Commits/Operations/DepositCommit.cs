using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01
{
    class DepositCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public async Task<XEvmDepositOperation> ApplyEvm(string hash, DelayedOperation deposit, JsonElement feederReceipt)
        {
            #region init
            var block = Context.Block;

            if (deposit is not DelayedXtzDeposit xtzDeposit)
                throw new InvalidOperationException("Invalid deposit type");

            var (type, amount, receiverAddress, inboxLevel, inboxMessageId) =
                (DepositType.Xtz, xtzDeposit.Amount, xtzDeposit.Receiver, xtzDeposit.InboxLevel, xtzDeposit.InboxMessageId);

            var receiver = await Helpers.GetOrCreateXEvmAddress(receiverAddress);

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

                #region crutch for nested proxy calls in old etherlink
                SenderId = (await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress)).Id,
                GasUsed = 0,
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

            Context.Block.Operations |= XOperations.Deposit;

            Cache.Chain.Get().DepositOpsCount++;
            #endregion

            #region apply result
            if (op.Status == OperationStatus.Applied)
            {
                Receive(receiver, op.Amount);
                Context.Statistics.TotalCreated += op.Amount;
            }
            #endregion

            Db.DepositOps.Add(op);
            Context.DepositOps.Add(op);

            return op;
        }

        public async Task Revert(XEvmDepositOperation op)
        {
            #region init
            var receiver = (await Cache.Addresses.GetAsync(op.ReceiverId) as XEvmAddress)!;

            Db.TryAttach(receiver);
            #endregion

            #region revert result
            if (op.Status == OperationStatus.Applied)
                RevertReceive(receiver, op.Amount);
            #endregion

            #region revert operation
            receiver.DepositOpsCount--;
            receiver.LastLevel = op.Level;
            receiver.LastTimestamp = op.Timestamp;
            if (receiver.IsEmpty()) await Helpers.RemoveXEvmAddress(receiver);

            Cache.Chain.Get().DepositOpsCount--;
            #endregion

            Db.DepositOps.Remove(op);
            Cache.Chain.ReleaseOperationId();
        }
    }
}
