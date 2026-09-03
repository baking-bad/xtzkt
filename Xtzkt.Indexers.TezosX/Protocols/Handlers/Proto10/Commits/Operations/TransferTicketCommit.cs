using System.Numerics;
using System.Text.Json;
using Netezos.Contracts;
using Netezos.Encoding;
using Netezos.Forging;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class TransferTicketCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public XTransferTicketOperation Operation { get; private set; } = null!;
    public IEnumerable<TicketUpdates>? TicketUpdates { get; private set; }

    public virtual async Task Apply(byte[] hash, JsonElement content, bool isDelayedOp, bool isFirstOp)
    {
        #region init
        var block = Context.Block;
        var senderAddress = content.RequiredString("source");
        var sender = await Helpers.GetOrCreateXMichelsonUser(senderAddress);

        var targetAddress = content.RequiredString("destination");
        var target = await Helpers.GetOrCreateXMichelsonAddress(targetAddress);

        var ticketerAddress = content.RequiredString("ticket_ticketer");
        var ticketer = await Helpers.GetOrCreateXMichelsonAddress(ticketerAddress);

        var metadata = content.Required("metadata");
        var result = metadata.Required("operation_result");

        var fee = content.RequiredInt64("fee");
        var counter = content.RequiredInt32("counter");
        var gasLimit = content.RequiredInt32("gas_limit");
        var storageLimit = content.RequiredInt32("storage_limit");
        var amount = BigInteger.Parse(content.RequiredString("amount"));
        var entrypoint = content.RequiredString("entrypoint");
        var ticketType = content.RequiredMicheline("ticket_ty");
        var ticketContent = content.RequiredMicheline("ticket_contents");
        var status = result.RequiredOpStatus("status");

        var daFee = 0L;
        if (!isDelayedOp)
        {
            var size = LocalForge.ForgeTransferTicket(new()
            {
                Source = senderAddress,
                Counter = counter,
                GasLimit = gasLimit,
                StorageLimit = storageLimit,
                Fee = fee,
                Destination = targetAddress,
                TicketTicketer = ticketerAddress,
                TicketAmount = amount,
                Entrypoint = entrypoint,
                TicketType = ticketType,
                TicketContent = ticketContent,
            }).Length;

            if (isFirstOp)
                size += 32 + (senderAddress.StartsWith("tz4") ? 96 : 64);

            daFee = size * Context.Protocol.DaFeePerByte;
        }
        var gasFee = fee - daFee;

        var gasFeeRefundedUpdate = metadata
            .OptionalArray("balance_updates")?
            .EnumerateArray()
            .FirstOrDefault(x =>
                x.RequiredString("kind") == "accumulator" &&
                x.RequiredString("category") == "block fees" &&
                x.RequiredInt64("change") < 0)
            ?? default;

        var gasFeeRefunded = gasFeeRefundedUpdate.ValueKind != JsonValueKind.Undefined
            ? -gasFeeRefundedUpdate.RequiredInt64("change")
            : 0;

        var paidStorageSizeDiff = result.OptionalInt32("paid_storage_size_diff");
        var (storageFee, _) = GetStorageFees(result, false, paidStorageSizeDiff);

        var op = new XTransferTicketOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            DaFee = daFee,
            GasFee = gasFee,
            GasFeeRefunded = gasFeeRefunded,
            Counter = counter,
            GasLimit = gasLimit,
            StorageLimit = storageLimit,
            SenderId = sender.Id,
            Status = status,
            Errors = result.TryGetProperty("errors", out var errors)
                ? OperationErrors.Parse(content, errors)
                : null,
            GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
            StorageUsed = paidStorageSizeDiff ?? 0,
            StorageFee = storageFee,
            Amount = amount,
            TicketerId = ticketer.Id,
            Entrypoint = entrypoint,
            TargetId = target.Id
        };

        try
        {
            var micheType = Schema.Create((ticketType as MichelinePrim)!);
            op.RawType = micheType.ToMicheline().ToBytes();
            op.RawContent = micheType.Optimize(ticketContent).ToBytes();
            op.JsonContent = Regexes.RestrictedUnicode().Replace(micheType.Humanize(ticketContent), Regexes.NullEscapeString);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "failed to process 'transfer_ticket' parameters");
        }
        #endregion

        #region apply operation
        Db.TryAttach(sender);
        PayFee(sender, op.DaFee);
        BurnFee(sender, op.GasFee - op.GasFeeRefunded);
        sender.Counter = op.Counter;
        sender.TransferTicketCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        if (target != sender)
        {
            Db.TryAttach(target);
            target.TransferTicketCount++;
            target.LastLevel = op.Level;
            target.LastTimestamp = op.Timestamp;
        }

        if (ticketer != sender && ticketer != target)
        {
            Db.TryAttach(ticketer);
            ticketer.TransferTicketCount++;
            ticketer.LastLevel = op.Level;
            ticketer.LastTimestamp = op.Timestamp;
        }

        block.MichelsonGasUsed += op.GasUsed;
        block.Operations |= XOperations.TransferTicket;

        Cache.Chain.Get().TransferTicketOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            BurnFee(sender, op.StorageFee ?? 0);

            TicketUpdates = ParseTicketUpdates(result);
        }
        #endregion

        Db.TransferTicketOps.Add(op);
        Context.TransferTicketOps.Add(op);
        Operation = op;
    }

    public virtual async Task Revert(XTransferTicketOperation operation)
    {
        #region entities
        var sender = (await Cache.Addresses.GetAsync(operation.SenderId) as XMichelsonUser)!;
        var target = (await Cache.Addresses.GetAsync(operation.TargetId) as XMichelsonAddress)!;
        var ticketer = (await Cache.Addresses.GetAsync(operation.TicketerId) as XMichelsonAddress)!;

        Db.TryAttach(sender);
        Db.TryAttach(target);
        Db.TryAttach(ticketer);
        #endregion

        #region revert result
        if (operation.Status == OperationStatus.Applied)
        {
            RevertBurnFee(sender, operation.StorageFee ?? 0);
        }
        #endregion

        #region revert operation
        RevertPayFee(sender, operation.DaFee);
        RevertBurnFee(sender, operation.GasFee - operation.GasFeeRefunded);
        sender.Counter = operation.Counter - 1;
        sender.Revealed = true;
        sender.TransferTicketCount--;
        sender.LastLevel = operation.Level;
        sender.LastTimestamp = operation.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXMichelsonUser(sender);

        if (target != sender)
        {
            target.TransferTicketCount--;
            target.LastLevel = operation.Level;
            target.LastTimestamp = operation.Timestamp;
            if (target.IsEmpty()) await Helpers.RemoveXMichelsonAddress(target);
        }

        if (ticketer != sender && ticketer != target)
        {
            ticketer.TransferTicketCount--;
            ticketer.LastLevel = operation.Level;
            ticketer.LastTimestamp = operation.Timestamp;
            if (ticketer.IsEmpty()) await Helpers.RemoveXMichelsonAddress(ticketer);
        }

        Cache.Chain.Get().TransferTicketOpsCount--;
        #endregion

        Db.TransferTicketOps.Remove(operation);
        Cache.Chain.ReleaseOperationId();
    }

    protected virtual IEnumerable<TicketUpdates>? ParseTicketUpdates(JsonElement result)
    {
        if (!result.TryGetProperty("ticket_updates", out var ticketUpdates))
            return null;

        var res = new List<TicketUpdates>();
        foreach (var updates in ticketUpdates.RequiredArray().EnumerateArray())
        {
            var list = new List<TicketUpdate>();
            foreach (var update in updates.RequiredArray("updates").EnumerateArray())
            {
                var amount = update.RequiredBigInteger("amount");
                if (amount != BigInteger.Zero)
                {
                    list.Add(new TicketUpdate
                    {
                        Address = update.RequiredString("account"),
                        Amount = amount
                    });
                }
            }

            if (list.Count > 0)
            {
                var ticketToken = updates.Required("ticket_token");
                var type = ticketToken.RequiredMicheline("content_type");
                var value = ticketToken.RequiredMicheline("content");
                var rawType = type.ToBytes();

                byte[] rawContent;
                string? jsonContent;

                try
                {
                    var schema = Schema.Create((type as MichelinePrim)!);
                    rawContent = schema.Optimize(value).ToBytes();
                    jsonContent = Regexes.RestrictedUnicode().Replace(schema.Humanize(value), Regexes.NullEscapeString);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to parse ticket content");
                    rawContent = value.ToBytes();
                    jsonContent = null;
                }

                res.Add(new TicketUpdates
                {
                    Ticket = new TicketIdentity
                    {
                        Ticketer = ticketToken.RequiredString("ticketer"),
                        RawType = rawType,
                        RawContent = rawContent,
                        JsonContent = jsonContent,
                    },
                    Updates = list
                });
            }
        }

        return res.Count > 0 ? res : null;
    }
}
