using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class DelegationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var prevBaker = sender.BakerId is int senderBakerId
                ? await Cache.Addresses.GetAsync(senderBakerId) as L1Baker
                : sender as L1Baker;
            var newBaker = content.OptionalString("delegate") is string _bakerAddress
                ? await Cache.Addresses.GetOrCreateAsync(_bakerAddress, block)
                : null;

            var result = content.Required("metadata").Required("operation_result");

            var delegation = new DelegationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = Context.Block.Level,
                Timestamp = Context.Block.Timestamp,
                Hash = opHash,
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                BakerId = newBaker?.Id,
                PrevBakerId = prevBaker?.Id,
                Amount = sender.Balance - content.RequiredInt64("fee"),
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
                GasUsed = GetConsumedGas(result)
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, delegation.BakerFee);
            sender.LastLevel = delegation.Level;
            sender.LastTimestamp = delegation.Timestamp;
            sender.Counter = delegation.Counter;
            sender.DelegationsCount++;

            if (prevBaker != null)
            {
                Db.TryAttach(prevBaker);
                prevBaker.LastLevel = delegation.Level;
                prevBaker.LastTimestamp = delegation.Timestamp;
                if (prevBaker != sender)
                    prevBaker.DelegationsCount++;
            }

            if (newBaker != null)
            {
                Db.TryAttach(newBaker);
                newBaker.LastLevel = delegation.Level;
                newBaker.LastTimestamp = delegation.Timestamp;
                if (newBaker != sender && newBaker != prevBaker)
                    newBaker.DelegationsCount++;
            }

            Context.Block.Operations |= L1Operations.Delegation;

            Cache.Chain.Get().DelegationOpsCount++;
            #endregion

            #region apply result
            if (delegation.Status == OperationStatus.Applied)
            {
                if (sender is L1Baker baker)
                {
                    #region reactivate baker
                    if (baker.DeactivationLevel <= delegation.Level)
                        await ActivateBaker(baker);

                    delegation.PrevDeactivationLevel = baker.DeactivationLevel;
                    baker.DeactivationLevel = GracePeriod.Init(Context.Block.Level, Context.Protocol);
                    #endregion
                }
                else
                {
                    if (prevBaker != null)
                    {
                        #region reset current delegation
                        if (result.TryGetProperty("balance_updates", out var updates))
                            await Unstake(delegation, [.. updates.EnumerateArray()]);

                        delegation.PrevDelegationLevel = sender.DelegationLevel;
                        delegation.PrevDelegationTimestamp = sender.DelegationTimestamp;

                        Undelegate(sender, prevBaker);
                        #endregion
                    }

                    if (sender == newBaker)
                    {
                        #region register baker
                        sender = newBaker = RegisterBaker((sender as L1User)!);

                        if (sender.OriginationsCount != 0)
                        {
                            var weirdOriginations = await Db.OriginationOps
                                .AsNoTracking()
                                .OfType<L1OriginationOperation>()
                                .Where(x => x.BakerId == sender.Id && x.Status == OperationStatus.Applied)
                                .ToListAsync();

                            foreach (var origination in weirdOriginations)
                            {
                                var weirdDelegator = await Cache.Addresses.GetAsync(origination.ContractId!.Value);
                                var hasDelegated = await Db.DelegationOps
                                    .AnyAsync(x => x.SenderId == weirdDelegator.Id && x.Status == OperationStatus.Applied);

                                if (!hasDelegated)
                                {
                                    Db.TryAttach(weirdDelegator);
                                    weirdDelegator.LastLevel = delegation.Level;
                                    weirdDelegator.LastTimestamp = delegation.Timestamp;
                                    Delegate(weirdDelegator, (sender as L1Baker)!, origination.Level, origination.Timestamp);
                                }
                            }
                        }
                        #endregion
                    }
                    else if (newBaker is L1Baker _newBaker)
                    {
                        Delegate(sender, _newBaker, delegation.Level, delegation.Timestamp);
                    }
                }
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.DelegationOps.Add(delegation);
            Context.DelegationOps.Add(delegation);
        }

        public virtual async Task ApplyInternal(L1Block block, IParentOperation parent, JsonElement content)
        {
            #region init
            var initiator = await Cache.Addresses.GetAsync(parent.SenderId);
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var prevBaker = sender.BakerId is int senderBakerId
                ? await Cache.Addresses.GetAsync(senderBakerId) as L1Baker
                : null;
            var newBaker = content.OptionalString("delegate") is string _bakerAddress
                ? await Cache.Addresses.GetOrCreateAsync(_bakerAddress, block)
                : null;

            var result = content.Required("result");

            var delegation = new DelegationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = parent.Level,
                Timestamp = parent.Timestamp,
                Hash = parent.Hash,
                Counter = parent.Counter,
                Nonce = content.RequiredInt32("nonce"),
                InitiatorId = initiator.Id,
                SenderId = sender.Id,
                SenderCodeHash = (sender as L1Contract)?.CodeHash,
                BakerId = newBaker?.Id,
                PrevBakerId = prevBaker?.Id,
                Amount = sender.Balance,
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
                GasUsed = GetConsumedGas(result)
            };
            #endregion

            #region apply operation
            parent.InternalOperations = (parent.InternalOperations ?? 0) + 1;

            Db.TryAttach(sender);
            sender.LastLevel = delegation.Level;
            sender.LastTimestamp = delegation.Timestamp;
            sender.DelegationsCount++;

            if (prevBaker != null)
            {
                Db.TryAttach(prevBaker);
                prevBaker.LastLevel = delegation.Level;
                prevBaker.LastTimestamp = delegation.Timestamp;
                if (prevBaker != sender)
                    prevBaker.DelegationsCount++;
            }

            if (newBaker != null)
            {
                Db.TryAttach(newBaker);
                newBaker.LastLevel = delegation.Level;
                newBaker.LastTimestamp = delegation.Timestamp;
                if (newBaker != sender && newBaker != prevBaker)
                    newBaker.DelegationsCount++;
            }

            if (initiator != sender && initiator != prevBaker && initiator != newBaker)
            {
                initiator.DelegationsCount++;
            }

            Context.Block.Operations |= L1Operations.Delegation;

            Cache.Chain.Get().DelegationOpsCount++;
            #endregion

            #region apply result
            if (delegation.Status == OperationStatus.Applied)
            {
                if (prevBaker != null)
                {
                    #region reset current delegation
                    //if (result.TryGetProperty("balance_updates", out var updates))
                    //    await Unstake(delegation, [.. updates.EnumerateArray()]);

                    delegation.PrevDelegationLevel = sender.DelegationLevel;
                    delegation.PrevDelegationTimestamp = sender.DelegationTimestamp;

                    Undelegate(sender, prevBaker);
                    #endregion
                }

                if (newBaker is L1Baker _newBaker)
                {
                    Delegate(sender, _newBaker, delegation.Level, delegation.Timestamp);
                }
            }
            #endregion

            Db.DelegationOps.Add(delegation);
            Context.DelegationOps.Add(delegation);
        }

        public virtual async Task Revert(L1Block block, DelegationOperation delegation)
        {
            #region init
            var sender = await Cache.Addresses.GetAsync(delegation.SenderId);
            var prevBaker = delegation.PrevBakerId is int prevBakerId
                ? await Cache.Addresses.GetAsync(prevBakerId) as L1Baker
                : null;
            var newBaker = delegation.BakerId is int bakerId
                ? await Cache.Addresses.GetAsync(bakerId)
                : null;

            Db.TryAttach(sender);
            Db.TryAttach(prevBaker);
            Db.TryAttach(newBaker);
            #endregion

            #region revert result
            if (delegation.Status == OperationStatus.Applied)
            {
                if (sender is L1Baker baker)
                {
                    if (delegation.PrevDeactivationLevel is int prevDeactivationLevel)
                    {
                        #region deactivate baker
                        if (delegation.PrevDeactivationLevel <= delegation.Level)
                            await DeactivateBaker(baker);

                        baker.DeactivationLevel = prevDeactivationLevel;
                        #endregion
                    }
                    else
                    {
                        #region unregister baker
                        if (baker.DelegatorsCount != 0)
                        {
                            var weirdOriginations = await Db.OriginationOps
                                .AsNoTracking()
                                .OfType<L1OriginationOperation>()
                                .Where(x => x.BakerId == baker.Id && x.Status == OperationStatus.Applied)
                                .ToListAsync();

                            foreach (var origination in weirdOriginations)
                            {
                                var weirdDelegator = await Cache.Addresses.GetAsync(origination.ContractId!.Value);
                                var delegated = await Db.DelegationOps
                                    .AnyAsync(x => x.SenderId == weirdDelegator.Id && x.Status == OperationStatus.Applied);

                                if (!delegated)
                                {
                                    Db.TryAttach(weirdDelegator);
                                    weirdDelegator.LastLevel = delegation.Level;
                                    weirdDelegator.LastTimestamp = delegation.Timestamp;
                                    Undelegate(weirdDelegator, baker);
                                }
                            }
                        }

                        sender = newBaker = UnregisterBaker(baker);

                        if (prevBaker != null)
                        {
                            Delegate(sender, prevBaker, delegation.PrevDelegationLevel!.Value, delegation.PrevDelegationTimestamp!.Value);
                            await RevertUnstake(delegation);
                        }
                        #endregion
                    }
                }
                else
                {
                    if (newBaker is L1Baker _newBaker)
                    {
                        Undelegate(sender, _newBaker);
                    }

                    if (prevBaker != null)
                    {
                        Delegate(sender, prevBaker, delegation.PrevDelegationLevel!.Value, delegation.PrevDelegationTimestamp!.Value);
                        await RevertUnstake(delegation);
                    }
                }
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, delegation.BakerFee);
            sender.LastLevel = delegation.Level;
            sender.LastTimestamp = delegation.Timestamp;
            sender.Counter = delegation.Counter - 1;
            if (sender is L1User user) user.Revealed = true;
            sender.DelegationsCount--;

            if (prevBaker != null)
            {
                prevBaker.LastLevel = delegation.Level;
                prevBaker.LastTimestamp = delegation.Timestamp;
                if (prevBaker != sender)
                    prevBaker.DelegationsCount--;
            }

            if (newBaker != null)
            {
                newBaker.LastLevel = delegation.Level;
                newBaker.LastTimestamp = delegation.Timestamp;
                if (newBaker != sender && newBaker != prevBaker)
                    newBaker.DelegationsCount--;
            }

            Cache.Chain.Get().DelegationOpsCount--;
            #endregion

            Db.DelegationOps.Remove(delegation);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }

        public virtual async Task RevertInternal(L1Block block, DelegationOperation delegation)
        {
            #region init
            var initiator = await Cache.Addresses.GetAsync(delegation.InitiatorId!.Value);
            var sender = await Cache.Addresses.GetAsync(delegation.SenderId);
            var prevBaker = delegation.PrevBakerId is int prevBakerId
                ? await Cache.Addresses.GetAsync(prevBakerId) as L1Baker
                : null;
            var newBaker = delegation.BakerId is int bakerId
                ? await Cache.Addresses.GetAsync(bakerId)
                : null;

            Db.TryAttach(initiator);
            Db.TryAttach(sender);
            Db.TryAttach(prevBaker);
            Db.TryAttach(newBaker);
            #endregion

            #region revert result
            if (delegation.Status == OperationStatus.Applied)
            {
                if (newBaker is L1Baker _newBaker)
                {
                    Undelegate(sender, _newBaker);
                }

                if (prevBaker != null)
                {
                    Delegate(sender, prevBaker, delegation.PrevDelegationLevel!.Value, delegation.PrevDelegationTimestamp!.Value);
                    //await RevertUnstake(delegation);
                }
            }
            #endregion

            #region revert operation
            sender.LastLevel = delegation.Level;
            sender.LastTimestamp = delegation.Timestamp;
            sender.DelegationsCount--;

            if (prevBaker != null)
            {
                prevBaker.LastLevel = delegation.Level;
                prevBaker.LastTimestamp = delegation.Timestamp;
                if (prevBaker != sender)
                    prevBaker.DelegationsCount--;
            }

            if (newBaker != null)
            {
                newBaker.LastLevel = delegation.Level;
                newBaker.LastTimestamp = delegation.Timestamp;
                if (newBaker != sender && newBaker != prevBaker)
                    newBaker.DelegationsCount--;
            }

            if (initiator != sender && initiator != prevBaker && initiator != newBaker)
            {
                initiator.DelegationsCount--;
            }

            Cache.Chain.Get().DelegationOpsCount--;
            #endregion

            Db.DelegationOps.Remove(delegation);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual int GetConsumedGas(JsonElement result)
        {
            return result.OptionalInt32("consumed_gas") ?? 0;
        }

        protected virtual Task Unstake(DelegationOperation op, List<JsonElement> balanceUpdates) => Task.CompletedTask;

        protected virtual Task RevertUnstake(DelegationOperation op) => Task.CompletedTask;
    }
}
