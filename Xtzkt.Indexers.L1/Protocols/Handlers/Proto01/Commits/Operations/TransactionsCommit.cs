using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class TransactionsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public L1TransactionOperation Transaction { get; private set; } = null!;
        public IEnumerable<BigMapDiff>? BigMapDiffs { get; private set; }
        public IEnumerable<TicketUpdates>? TicketUpdates { get; private set; }
        public L1Address? Target { get; private set; }

        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var target = await Cache.Addresses.GetOrCreateAsync(content.RequiredString("destination"), block);

            var result = content.Required("metadata").Required("operation_result");

            var transaction = new L1TransactionOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                Amount = content.RequiredInt64("amount"),
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                TargetId = target.Id,
                TargetCodeHash = (target as L1Contract)?.CodeHash,
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
                GasUsed = GetConsumedGas(result),
                StorageUsed = result.OptionalInt32("paid_storage_size_diff") ?? 0,
                StorageFee = result.OptionalInt32("paid_storage_size_diff") > 0
                    ? result.OptionalInt32("paid_storage_size_diff") * Context.Protocol.ByteCost
                    : null,
                AllocationFee = HasAllocated(result)
                    ? (long?)Context.Protocol.OriginationSize * Context.Protocol.ByteCost
                    : null
            };


            if (target is not L1User && content.TryGetProperty("parameters", out var parameters))
                await ProcessParameters(transaction, target, parameters);
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, transaction.BakerFee.Value);
            sender.Counter = transaction.Counter;
            sender.TransactionsCount++;

            Db.TryAttach(target);
            if (target != sender)
                target.TransactionsCount++;

            block.GasUsed += transaction.GasUsed;
            block.Operations |= L1Operations.Transaction;

            Cache.Chain.Get().TransactionOpsCount++;
            #endregion

            #region apply result
            if (transaction.Status == OperationStatus.Applied)
            {
                var burned = (transaction.StorageFee ?? 0) + (transaction.AllocationFee ?? 0);
                Proto.Manager.Burn(burned);
                BurnFeeAndSpend(sender, burned, transaction.Amount);
                Receive(target, transaction.Amount);

                await ResetGracePeriod(transaction, target);

                if (result.TryGetProperty("storage", out var storage))
                {
                    BigMapDiffs = ParseBigMapDiffs(transaction, result);
                    await ProcessStorage(transaction, target, storage);
                }

                await ApplyAddressRegistryDiffs(transaction, result);

                TicketUpdates = ParseTicketUpdates("ticket_updates", result);
                
                if (target is L1SmartRollup)
                    Proto.Inbox.Push(transaction.Id);

                if (target.Id == NullAddress.Id)
                    Cache.Statistics.Current.TotalBanished += transaction.Amount;
            }
            #endregion

            Proto.Manager.Set(sender);
            Proto.Manager.Add(transaction);
            //Db.TransactionOps.Add(transaction);
            Context.TransactionOps.Add(transaction);
            Transaction = transaction;
            Target = target;
        }

        public virtual async Task ApplyInternal(L1Block block, IParentOperation parent, JsonElement content)
        {
            #region init
            var parentSender = await Cache.Addresses.GetAsync(parent.SenderId);
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var target = await Cache.Addresses.GetOrCreateAsync(content.RequiredString("destination"), block);

            var result = content.Required("result");

            var transaction = new L1TransactionOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                InitiatorId = parent.SenderId,
                Level = parent.Level,
                Timestamp = parent.Timestamp,
                Hash = parent.Hash,
                Counter = parent.Counter,
                Amount = content.RequiredInt64("amount"),
                Nonce = content.RequiredInt32("nonce"),
                SenderId = sender.Id,
                SenderCodeHash = (sender as L1Contract)?.CodeHash,
                TargetId = target.Id,
                TargetCodeHash = (target as L1Contract)?.CodeHash,
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
                GasUsed = GetConsumedGas(result),
                StorageUsed = result.OptionalInt32("paid_storage_size_diff") ?? 0,
                StorageFee = result.OptionalInt32("paid_storage_size_diff") > 0
                    ? result.OptionalInt32("paid_storage_size_diff") * Context.Protocol.ByteCost
                    : null,
                AllocationFee = HasAllocated(result)
                    ? (long?)Context.Protocol.OriginationSize * Context.Protocol.ByteCost
                    : null
            };

            if (target is not L1User && content.TryGetProperty("parameters", out var parameters))
                await ProcessParameters(transaction, target, parameters);
            #endregion

            #region apply operation
            parent.InternalOperations = (parent.InternalOperations ?? 0) + 1;

            Db.TryAttach(sender);
            sender.TransactionsCount++;

            Db.TryAttach(target);
            if (target != sender)
                target.TransactionsCount++;

            if (parentSender != sender && parentSender != target)
                parentSender.TransactionsCount++;

            block.GasUsed += transaction.GasUsed;
            block.Operations |= L1Operations.Transaction;

            Cache.Chain.Get().TransactionOpsCount++;
            #endregion

            #region apply result
            if (transaction.Status == OperationStatus.Applied)
            {
                var burned = (transaction.StorageFee ?? 0) + (transaction.AllocationFee ?? 0);
                Proto.Manager.Burn(burned);
                BurnFee(parentSender, burned);
                Spend(sender, transaction.Amount);
                Receive(target, transaction.Amount);

                if (target == parentSender)
                    Proto.Manager.Credit(transaction.Amount);

                await ResetGracePeriod(transaction, target);

                if (result.TryGetProperty("storage", out var storage))
                {
                    BigMapDiffs = ParseBigMapDiffs(transaction, result);
                    await ProcessStorage(transaction, target, storage);
                }

                await ApplyAddressRegistryDiffs(transaction, result);

                TicketUpdates = ParseTicketUpdates("ticket_receipt", result);

                if (target is L1SmartRollup)
                    Proto.Inbox.Push(transaction.Id);

                if (target.Id == NullAddress.Id)
                    Cache.Statistics.Current.TotalBanished += transaction.Amount;
            }
            #endregion

            //Db.TransactionOps.Add(transaction);
            Context.TransactionOps.Add(transaction);
            Transaction = transaction;
            Target = target;
        }

        public virtual async Task Revert(L1Block block, L1TransactionOperation transaction)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(transaction.SenderId);
            var target = await Cache.Addresses.GetAsync(transaction.TargetId);

            Db.TryAttach(sender);
            Db.TryAttach(target);
            #endregion

            #region revert result
            if (transaction.Status == OperationStatus.Applied)
            {
                RevertReceive(target, transaction.Amount);
                
                if (target is L1Baker baker)
                {
                    if (transaction.ResetDeactivation != null)
                    {
                        if (transaction.ResetDeactivation <= transaction.Level)
                            await DeactivateBaker(baker);

                        baker.DeactivationLevel = (int)transaction.ResetDeactivation;
                    }
                }

                RevertBurnFeeAndSpend(sender, (transaction.StorageFee ?? 0) + (transaction.AllocationFee ?? 0), transaction.Amount);

                if (transaction.StorageId != null)
                    await RevertStorage(transaction, (target as L1Contract)!);

                await RevertAddressRegistryDiffs(transaction);
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, transaction.BakerFee!.Value);

            sender.TransactionsCount--;
            if (target != sender) target.TransactionsCount--;

            sender.Counter = transaction.Counter - 1;
            if (sender is L1User user) user.Revealed = true;

            Cache.Chain.Get().TransactionOpsCount--;
            #endregion

            //Db.TransactionOps.Remove(transaction);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }

        public virtual async Task RevertInternal(L1Block block, L1TransactionOperation transaction)
        {
            #region entities
            var parentSender = await Cache.Addresses.GetAsync(transaction.InitiatorId!.Value);
            var sender = await Cache.Addresses.GetAsync(transaction.SenderId);
            var target = await Cache.Addresses.GetAsync(transaction.TargetId);

            Db.TryAttach(parentSender);
            Db.TryAttach(sender);
            Db.TryAttach(target);
            #endregion

            #region revert result
            if (transaction.Status == OperationStatus.Applied)
            {
                RevertReceive(target, transaction.Amount);

                if (target is L1Baker baker)
                {
                    if (transaction.ResetDeactivation != null)
                    {
                        if (transaction.ResetDeactivation <= transaction.Level)
                            await DeactivateBaker(baker);

                        baker.DeactivationLevel = (int)transaction.ResetDeactivation;
                    }
                }

                RevertSpend(sender, transaction.Amount);
                RevertBurnFee(parentSender, (transaction.StorageFee ?? 0) + (transaction.AllocationFee ?? 0));

                if (transaction.StorageId != null)
                    await RevertStorage(transaction, (target as L1Contract)!);

                await RevertAddressRegistryDiffs(transaction);
            }
            #endregion

            #region revert operation
            sender.TransactionsCount--;
            if (target != sender) target.TransactionsCount--;
            if (parentSender != sender && parentSender != target) parentSender.TransactionsCount--;

            Cache.Chain.Get().TransactionOpsCount--;
            #endregion

            //Db.TransactionOps.Remove(transaction);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual bool HasAllocated(JsonElement result) => false;

        protected virtual async Task ResetGracePeriod(L1TransactionOperation transaction, L1Address target)
        {
            if (target is L1Baker baker)
            {
                var newDeactivationLevel = baker.Staked ? GracePeriod.Reset(transaction.Level, Context.Protocol) : GracePeriod.Init(transaction.Level, Context.Protocol);
                if (baker.DeactivationLevel < newDeactivationLevel)
                {
                    if (baker.DeactivationLevel <= transaction.Level)
                        await ActivateBaker(baker);

                    transaction.ResetDeactivation = baker.DeactivationLevel;
                    baker.DeactivationLevel = newDeactivationLevel;
                }
            }
        }

        protected virtual async Task ProcessParameters(L1TransactionOperation transaction, L1Address target, JsonElement parameters)
        {
            var (rawEp, rawParam) = ("default", Micheline.FromJson(parameters)!);

            if (target is L1Contract contract)
            {
                if (contract.Kind == L1ContractKind.DelegatorContract)
                {
                    if (rawParam is MichelinePrim p && p.Prim == PrimType.Unit)
                        return;

                    transaction.Entrypoint = rawEp;
                    transaction.ParametersRaw = rawParam.ToBytes();
                }
                else
                {
                    try
                    {
                        var schema = await Cache.Schemas.GetAsync(contract);
                        transaction.Guessed = false;

                        var (normEp, normParam) = schema.NormalizeParameter(rawEp, rawParam);

                        transaction.Entrypoint = normEp;
                        transaction.ParametersRaw = schema.OptimizeParameter(normEp, normParam).ToBytes();
                        transaction.Parameters = Regexes.RestrictedUnicode().Replace(schema.HumanizeParameter(normEp, normParam), Regexes.NullEscapeString);
                    }
                    catch (Exception ex)
                    {
                        transaction.Entrypoint ??= rawEp;
                        transaction.ParametersRaw ??= rawParam.ToBytes();

                        if (transaction.Status == OperationStatus.Applied)
                            Logger.LogError(ex, "Failed to humanize tx parameters");
                    }
                }
            }
            else
            {
                transaction.Entrypoint = rawEp;
                transaction.ParametersRaw = rawParam.ToBytes();
            }
        }

        protected virtual async Task ProcessStorage(L1TransactionOperation transaction, L1Address target, JsonElement storage)
        {
            if (target is not L1Contract contract || contract.Kind == L1ContractKind.DelegatorContract)
                return;

            var schema = await Cache.Schemas.GetAsync(contract);
            var currentStorage = await Cache.Storages.GetAsync(contract);

            var newStorageMicheline = schema.OptimizeStorage(Micheline.FromJson(storage)!, false);
            newStorageMicheline = NormalizeStorage(transaction, newStorageMicheline, schema);
            var newStorageBytes = newStorageMicheline.ToBytes();

            if (newStorageBytes.IsEqual(currentStorage.RawValue))
            {
                transaction.StorageId = currentStorage.Id;
                return;
            }

            Db.TryAttach(currentStorage);
            currentStorage.Current = false;

            var newStorage = new Storage
            {
                Id = Cache.Chain.NextStorageId(),
                ChainId = contract.ChainId,
                Level = transaction.Level,
                ContractId = contract.Id,
                TransactionId = transaction.Id,
                RawValue = newStorageBytes,
                JsonValue = Regexes.RestrictedUnicode().Replace(schema.HumanizeStorage(newStorageMicheline), Regexes.NullEscapeString),
                Current = true,
            };

            Db.Storages.Add(newStorage);
            Cache.Storages.Add(contract, newStorage);

            transaction.StorageId = newStorage.Id;
        }

        public async Task RevertStorage(L1TransactionOperation transaction, L1Contract contract)
        {
            var storage = await Cache.Storages.GetAsync(contract);
            if (storage.TransactionId == transaction.Id)
            {
                var prevStorage = await Db.Storages
                    .Where(x => x.ContractId == contract.Id && x.Id < storage.Id)
                    .OrderByDescending(x => x.Id)
                    .FirstAsync();

                prevStorage.Current = true;
                Cache.Storages.Add(contract, prevStorage);

                Db.Storages.Remove(storage);
                Cache.Chain.ReleaseStorageId();
            }
        }

        protected virtual IMicheline NormalizeStorage(L1TransactionOperation transaction, IMicheline storage, ContractScript schema)
        {
            var view = schema.Storage.Schema.ToTreeView(storage);
            var bigmap = view.Nodes().FirstOrDefault(x => x.Schema.Prim == PrimType.big_map);
            if (bigmap != null)
                storage = storage.Replace(bigmap.Value, new MichelineInt(transaction.TargetId));
            return storage;
        }

        protected virtual IEnumerable<BigMapDiff>? ParseBigMapDiffs(L1TransactionOperation transaction, JsonElement result)
        {
            if (transaction.Level != 5993)
                return null;
            // It seems there were no big_map diffs at all in proto 1
            // thus there was no an adequate way to track big_map updates,
            // so the only way to handle this single big_map update is hardcoding
            return
            [
                new UpdateDiff
                {
                    Ptr = transaction.TargetId,
                    KeyHash = Hashes.ParseExprHash("exprteAx9hWkXvYSQ4nN9SqjJGVR1sTneHQS1QEcSdzckYdXZVvsqY"),
                    Key = new MichelineString("KT1R3uoZ6W1ZxEwzqtv75Ro7DhVY6UAcxuK2"),
                    Value = new MichelinePrim
                    {
                        Prim = PrimType.Pair,
                        Args =
                        [
                            new MichelineString("Aliases Contract"),
                            new MichelinePrim
                            {
                                Prim = PrimType.Pair,
                                Args =
                                [
                                    new MichelinePrim { Prim = PrimType.None },
                                    new MichelinePrim
                                    {
                                        Prim = PrimType.Pair,
                                        Args =
                                        [
                                            new MichelineInt(0),
                                            new MichelinePrim
                                            {
                                                Prim = PrimType.Pair,
                                                Args =
                                                [
                                                    new MichelinePrim
                                                    {
                                                        Prim = PrimType.Left,
                                                        Args =
                                                        [
                                                            new MichelinePrim { Prim = PrimType.Unit }
                                                        ]
                                                    },
                                                    new MichelineInt(1530741267)
                                                ]
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    },
                }
            ];
        }

        protected virtual int GetConsumedGas(JsonElement result)
        {
            return result.OptionalInt32("consumed_gas") ?? 0;
        }

        protected virtual IEnumerable<TicketUpdates>? ParseTicketUpdates(string property, JsonElement result)
        {
            if (!result.TryGetProperty(property, out var ticketUpdates))
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
                    var type = Micheline.FromJson(ticketToken.Required("content_type"))!;
                    var value = Micheline.FromJson(ticketToken.Required("content"))!;
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

        protected virtual Task ApplyAddressRegistryDiffs(L1TransactionOperation transaction, JsonElement result) => Task.CompletedTask;

        protected virtual Task RevertAddressRegistryDiffs(L1TransactionOperation transaction) => Task.CompletedTask;
    }
}
