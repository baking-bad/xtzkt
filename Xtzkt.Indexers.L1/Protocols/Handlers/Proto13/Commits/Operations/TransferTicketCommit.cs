using System.Numerics;
using System.Text.Json;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto13
{
    class TransferTicketCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public L1TransferTicketOperation Operation { get; private set; } = null!;
        public IEnumerable<TicketUpdates>? TicketUpdates { get; private set; }

        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var target = await Cache.Addresses.GetOrCreateAsync(content.RequiredString("destination"), block);
            var ticketer = await Cache.Addresses.GetOrCreateAsync(content.RequiredString("ticket_ticketer"), block);

            var result = content.Required("metadata").Required("operation_result");

            var operation = new L1TransferTicketOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                Status = result.RequiredString("status") switch
                {
                    "applied" => OperationStatus.Applied,
                    "backtracked" => OperationStatus.Backtracked,
                    "failed" => OperationStatus.Failed,
                    "skipped" => OperationStatus.Skipped,
                    _ => throw new NotImplementedException()
                },
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
                StorageUsed = result.OptionalInt32("paid_storage_size_diff") ?? 0,
                StorageFee = result.OptionalInt32("paid_storage_size_diff") > 0
                    ? result.OptionalInt32("paid_storage_size_diff") * Context.Protocol.ByteCost
                    : null,
                Amount = BigInteger.Parse(content.RequiredString("ticket_amount")),
                TicketerId = ticketer.Id,
                Entrypoint = content.RequiredString("entrypoint"),
                TargetId = target.Id
            };

            try
            {
                var micheType = Schema.Create((content.RequiredMicheline("ticket_ty") as MichelinePrim)!);
                var value = content.RequiredMicheline("ticket_contents");
                operation.RawType = micheType.ToMicheline().ToBytes();
                operation.RawContent = micheType.Optimize(value).ToBytes();
                operation.JsonContent = Regexes.RestrictedUnicode().Replace(micheType.Humanize(value), Regexes.NullEscapeString);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "failed to process 'transfer_ticket' parameters");
            }
            #endregion

            #region entities
            Db.TryAttach(sender);
            Db.TryAttach(target);
            Db.TryAttach(ticketer);
            #endregion

            #region apply operation
            PayFee(sender, operation.BakerFee);

            sender.TransferTicketCount++;
            if (target != sender) target.TransferTicketCount++;
            if (ticketer != sender && ticketer != target) ticketer.TransferTicketCount++;

            block.GasUsed += operation.GasUsed;
            block.Operations |= L1Operations.TransferTicket;

            sender.Counter = operation.Counter;

            Cache.Chain.Get().TransferTicketOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                var burned = operation.StorageFee ?? 0;
                Proto.Manager.Burn(burned);
                BurnFee(sender, burned);
                
                TicketUpdates = ParseTicketUpdates(result);
            }
            #endregion

            Proto.Manager.Set(sender);
            Proto.Manager.Add(operation);
            Db.TransferTicketOps.Add(operation);
            Context.TransferTicketOps.Add(operation);
            Operation = operation;
        }

        public virtual async Task Revert(L1Block block, L1TransferTicketOperation operation)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(operation.SenderId);
            var target = await Cache.Addresses.GetAsync(operation.TargetId);
            var ticketer = await Cache.Addresses.GetAsync(operation.TicketerId);

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
            RevertPayFee(sender, operation.BakerFee);

            sender.TransferTicketCount--;
            if (target != sender) target.TransferTicketCount--;
            if (ticketer != sender && ticketer != target) ticketer.TransferTicketCount--;

            sender.Counter = operation.Counter - 1;
            (sender as L1User)!.Revealed = true;

            Cache.Chain.Get().TransferTicketOpsCount--;
            #endregion

            Db.TransferTicketOps.Remove(operation);
            Cache.Chain.ReleaseManagerCounter();
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
}
