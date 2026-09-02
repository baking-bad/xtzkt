using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class OriginationsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public L1OriginationOperation Origination { get; private set; } = null!;
        public IEnumerable<BigMapDiff>? BigMapDiffs { get; private set; }
        public L1Contract? Contract { get; private set; }

        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var contractBaker = content.OptionalString("delegate") is string _bakerAddress
                ? await Cache.Addresses.GetOrCreateAsync(_bakerAddress, block)
                : null;

            var result = content.Required("metadata").Required("operation_result");

            var origination = new L1OriginationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = opHash,
                Balance = content.RequiredInt64("balance"),
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                BakerId = contractBaker?.Id,
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
                AllocationFee = Context.Protocol.OriginationSize * Context.Protocol.ByteCost
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, origination.BakerFee);
            sender.LastLevel = origination.Level;
            sender.LastTimestamp = origination.Timestamp;
            sender.Counter = origination.Counter;
            sender.OriginationsCount++;

            if (contractBaker != null)
            {
                Db.TryAttach(contractBaker);
                contractBaker.LastLevel = block.Level;
                contractBaker.LastTimestamp = block.Timestamp;
                if (contractBaker != sender)
                    contractBaker.OriginationsCount++;
            }

            Context.Block.Operations |= L1Operations.Origination;

            Cache.Chain.Get().OriginationOpsCount++;
            #endregion

            #region apply result
            if (origination.Status == OperationStatus.Applied)
            {
                var burned = (origination.StorageFee ?? 0) + (origination.AllocationFee ?? 0);
                Proto.Manager.Burn(burned);
                BurnFeeAndSpend(sender, burned, origination.Balance);
                sender.ContractsCount++;

                L1Contract contract;
                var contractAddress = result.RequiredArray("originated_contracts", 1)[0].RequiredString();
                if (await Cache.Addresses.GetAsync(contractAddress, block) is L1Address ghost)
                {
                    contract = new L1Contract
                    {
                        Id = ghost.Id,
                        ChainId = ghost.ChainId,
                        Index = ghost.Index,
                        FirstLevel = ghost.FirstLevel,
                        FirstTimestamp = ghost.FirstTimestamp,
                        LastLevel = origination.Level,
                        LastTimestamp = origination.Timestamp,
                        Hash = contractAddress,
                        CreatorId = sender.Id,
                        Kind = L1ContractKind.SmartContract,
                        OriginationsCount = 1,
                        ActiveTokensCount = ghost.ActiveTokensCount,
                        TokenBalancesCount = ghost.TokenBalancesCount,
                        TokenTransfersCount = ghost.TokenTransfersCount,
                        ActiveTicketsCount = ghost.ActiveTicketsCount,
                        TicketBalancesCount = ghost.TicketBalancesCount,
                        TicketTransfersCount = ghost.TicketTransfersCount
                    };
                    var isAdded = Db.Entry(ghost).State == EntityState.Added;
                    Db.Entry(ghost).State = EntityState.Detached;
                    Db.Entry(contract).State = isAdded ? EntityState.Added : EntityState.Modified;
                }
                else
                {
                    contract = new L1Contract
                    {
                        Id = Cache.Chain.NextAddressId(),
                        ChainId = Cache.Chain.Get().Id,
                        FirstLevel = origination.Level,
                        FirstTimestamp = origination.Timestamp,
                        LastLevel = origination.Level,
                        LastTimestamp = origination.Timestamp,
                        Hash = contractAddress,
                        CreatorId = sender.Id,
                        Kind = L1ContractKind.SmartContract,
                        OriginationsCount = 1
                    };
                    Db.Addresses.Add(contract);
                }
                Receive(contract, origination.Balance);
                Cache.Addresses.Add(contract);
                origination.ContractId = contract.Id;
                Contract = contract;

                if (contractBaker is L1Baker _contractDelegate)
                    Delegate(contract, _contractDelegate, origination.Level, origination.Timestamp);

                var code = await ExpandCode(contract, GetCode(content));
                var storage = GetStorage(content);

                BigMapDiffs = ParseBigMapDiffs(origination, result, code, storage);
                await ProcessScript(origination, contract, code, storage);
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.OriginationOps.Add(origination);
            Context.OriginationOps.Add(origination);
            Origination = origination;
        }

        public virtual async Task ApplyInternal(L1Block block, IParentOperation parent, JsonElement content)
        {
            #region init
            var initiator = await Cache.Addresses.GetAsync(parent.SenderId);
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            var contractBaker = content.OptionalString("delegate") is string _bakerAddress
                ? await Cache.Addresses.GetOrCreateAsync(_bakerAddress, block)
                : null;

            var result = content.Required("result");

            var origination = new L1OriginationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                InitiatorId = parent.SenderId,
                Level = parent.Level,
                Timestamp = parent.Timestamp,
                Hash = parent.Hash,
                Counter = parent.Counter,
                Nonce = content.RequiredInt32("nonce"),
                Balance = content.RequiredInt64("balance"),
                SenderId = sender.Id,
                SenderCodeHash = (sender as L1Contract)?.CodeHash,
                BakerId = contractBaker?.Id,
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
                AllocationFee = Context.Protocol.OriginationSize * Context.Protocol.ByteCost
            };
            #endregion

            #region apply operation
            parent.InternalOperations = (parent.InternalOperations ?? 0) + 1;

            Db.TryAttach(sender);
            sender.LastLevel = block.Level;
            sender.LastTimestamp = block.Timestamp;
            sender.OriginationsCount++;

            if (contractBaker != null)
            {
                Db.TryAttach(contractBaker);
                contractBaker.LastLevel = block.Level;
                contractBaker.LastTimestamp = block.Timestamp;
                if (contractBaker != sender)
                    contractBaker.OriginationsCount++;
            }

            if (initiator != sender && initiator != contractBaker)
            {
                initiator.OriginationsCount++;
            }

            block.Operations |= L1Operations.Origination;

            Cache.Chain.Get().OriginationOpsCount++;
            #endregion

            #region apply result
            if (origination.Status == OperationStatus.Applied)
            {
                var burned = (origination.StorageFee ?? 0) + (origination.AllocationFee ?? 0);
                Proto.Manager.Burn(burned);
                BurnFee(initiator, burned);

                Spend(sender, origination.Balance);
                sender.ContractsCount++;

                L1Contract contract;
                var contractAddress = result.RequiredArray("originated_contracts", 1)[0].RequiredString();
                if (await Cache.Addresses.GetAsync(contractAddress, block) is L1Address ghost)
                {
                    contract = new L1Contract
                    {
                        Id = ghost.Id,
                        ChainId = ghost.ChainId,
                        Index = ghost.Index,
                        FirstLevel = ghost.FirstLevel,
                        FirstTimestamp = ghost.FirstTimestamp,
                        LastLevel = origination.Level,
                        LastTimestamp = origination.Timestamp,
                        Hash = contractAddress,
                        Counter = 0,
                        CreatorId = sender.Id,
                        Kind = L1ContractKind.SmartContract,
                        OriginationsCount = 1,
                        ActiveTokensCount = ghost.ActiveTokensCount,
                        TokenBalancesCount = ghost.TokenBalancesCount,
                        TokenTransfersCount = ghost.TokenTransfersCount,
                        ActiveTicketsCount = ghost.ActiveTicketsCount,
                        TicketBalancesCount = ghost.TicketBalancesCount,
                        TicketTransfersCount = ghost.TicketTransfersCount
                    };
                    var isAdded = Db.Entry(ghost).State == EntityState.Added;
                    Db.Entry(ghost).State = EntityState.Detached;
                    Db.Entry(contract).State = isAdded ? EntityState.Added : EntityState.Modified;
                }
                else
                {
                    contract = new L1Contract
                    {
                        Id = Cache.Chain.NextAddressId(),
                        ChainId = Cache.Chain.Get().Id,
                        FirstLevel = origination.Level,
                        FirstTimestamp = origination.Timestamp,
                        LastLevel = origination.Level,
                        LastTimestamp = origination.Timestamp,
                        Hash = contractAddress,
                        Counter = 0,
                        CreatorId = sender.Id,
                        Kind = L1ContractKind.SmartContract,
                        OriginationsCount = 1
                    };
                    Db.Addresses.Add(contract);
                }
                Receive(contract, origination.Balance);
                Cache.Addresses.Add(contract);
                origination.ContractId = contract.Id;
                Contract = contract;

                if (contractBaker is L1Baker _contractDelegate)
                    Delegate(contract, _contractDelegate, origination.Level, origination.Timestamp);

                var code = await ExpandCode(contract, GetCode(content));
                var storage = GetStorage(content);

                BigMapDiffs = ParseBigMapDiffs(origination, result, code, storage);
                await ProcessScript(origination, contract, code, storage);
            }
            #endregion

            Db.OriginationOps.Add(origination);
            Context.OriginationOps.Add(origination);
            Origination = origination;
        }

        public virtual async Task Revert(L1Block block, L1OriginationOperation origination)
        {
            #region init
            var sender = await Cache.Addresses.GetAsync(origination.SenderId);
            var contractBaker = origination.BakerId is int bakerId
                ? await Cache.Addresses.GetAsync(bakerId)
                : null;
            var contract = origination.ContractId is int contractId
                ? await Cache.Addresses.GetAsync(contractId) as L1Contract
                : null;

            Db.TryAttach(sender);
            Db.TryAttach(contractBaker);
            Db.TryAttach(contract);
            #endregion

            #region revert result
            if (origination.Status == OperationStatus.Applied)
            {
                await RevertScript(origination, contract!);

                if (contractBaker is L1Baker _contractDelegate)
                    Undelegate(contract!, _contractDelegate);

                contract!.OriginationsCount--;
                if (contract.OriginationsCount == 0 &&
                    contract.TransactionsCount == 0 &&
                    contract.TransferTicketCount == 0 &&
                    contract.IncreasePaidStorageCount == 0 &&
                    contract.TokenTransfersCount == 0 &&
                    contract.TicketTransfersCount == 0 &&
                    contract.Index is null)
                {
                    Db.Addresses.Remove(contract);
                    Cache.Addresses.Remove(contract);
                }
                else
                {
                    var ghost = new L1Ghost
                    {
                        Id = contract.Id,
                        ChainId = contract.ChainId,
                        Balance = contract.Balance,
                        Index = contract.Index,
                        Hash = contract.Hash,
                        FirstLevel = contract.FirstLevel,
                        FirstTimestamp = contract.FirstTimestamp,
                        LastLevel = origination.Level,
                        LastTimestamp = origination.Timestamp,
                        OriginationsCount = contract.OriginationsCount,
                        TransactionsCount = contract.TransactionsCount,
                        TransferTicketCount = contract.TransferTicketCount,
                        IncreasePaidStorageCount = contract.IncreasePaidStorageCount,
                        ActiveTokensCount = contract.ActiveTokensCount,
                        TokenBalancesCount = contract.TokenBalancesCount,
                        TokenTransfersCount = contract.TokenTransfersCount,
                        ActiveTicketsCount = contract.ActiveTicketsCount,
                        TicketBalancesCount = contract.TicketBalancesCount,
                        TicketTransfersCount = contract.TicketTransfersCount,
                    };

                    Db.Entry(contract).State = EntityState.Detached;
                    Db.Entry(ghost).State = EntityState.Modified;
                    Cache.Addresses.Add(ghost);
                }

                RevertBurnFeeAndSpend(sender, (origination.StorageFee ?? 0) + (origination.AllocationFee ?? 0), origination.Balance);
                sender.ContractsCount--;
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, origination.BakerFee);
            sender.LastLevel = block.Level;
            sender.LastTimestamp = block.Timestamp;
            sender.Counter = origination.Counter - 1;
            if (sender is L1User user) user.Revealed = true;
            sender.OriginationsCount--;

            if (contractBaker != null)
            {
                contractBaker.LastLevel = block.Level;
                contractBaker.LastTimestamp = block.Timestamp;
                if (contractBaker != sender)
                    contractBaker.OriginationsCount--;
            }

            Cache.Chain.Get().OriginationOpsCount--;
            #endregion

            Db.OriginationOps.Remove(origination);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }

        public virtual async Task RevertInternal(L1Block block, L1OriginationOperation origination)
        {
            #region init
            var initiator = await Cache.Addresses.GetAsync(origination.InitiatorId!.Value);
            var sender = await Cache.Addresses.GetAsync(origination.SenderId);
            var contractBaker = origination.BakerId is int bakerId
                ? await Cache.Addresses.GetAsync(bakerId)
                : null;
            var contract = origination.ContractId is int contractId
                ? await Cache.Addresses.GetAsync(contractId) as L1Contract
                : null;

            Db.TryAttach(initiator);
            Db.TryAttach(sender);
            Db.TryAttach(contractBaker);
            Db.TryAttach(contract);
            #endregion

            #region revert result
            if (origination.Status == OperationStatus.Applied)
            {
                await RevertScript(origination, contract!);

                if (contractBaker is L1Baker _contractDelegate)
                    Undelegate(contract!, _contractDelegate);

                contract!.OriginationsCount--;
                if (contract.OriginationsCount == 0 &&
                    contract.TransactionsCount == 0 &&
                    contract.TransferTicketCount == 0 &&
                    contract.IncreasePaidStorageCount == 0 &&
                    contract.TokenTransfersCount == 0 &&
                    contract.TicketTransfersCount == 0 &&
                    contract.Index is null)
                {
                    Db.Addresses.Remove(contract);
                    Cache.Addresses.Remove(contract);
                }
                else
                {
                    var ghost = new L1Ghost
                    {
                        Id = contract.Id,
                        ChainId = contract.ChainId,
                        Balance = contract.Balance,
                        Index = contract.Index,
                        Hash = contract.Hash,
                        FirstLevel = contract.FirstLevel,
                        FirstTimestamp = contract.FirstTimestamp,
                        LastLevel = origination.Level,
                        LastTimestamp = origination.Timestamp,
                        ActiveTokensCount = contract.ActiveTokensCount,
                        TokenBalancesCount = contract.TokenBalancesCount,
                        TokenTransfersCount = contract.TokenTransfersCount,
                        ActiveTicketsCount = contract.ActiveTicketsCount,
                        TicketBalancesCount = contract.TicketBalancesCount,
                        TicketTransfersCount = contract.TicketTransfersCount,
                    };

                    Db.Entry(contract).State = EntityState.Detached;
                    Db.Entry(ghost).State = EntityState.Modified;
                    Cache.Addresses.Add(ghost);
                }

                RevertBurnFee(initiator, (origination.StorageFee ?? 0) + (origination.AllocationFee ?? 0));
                RevertSpend(sender, origination.Balance);
                sender.ContractsCount--;
            }
            #endregion

            #region revert operation
            sender.LastLevel = block.Level;
            sender.LastTimestamp = block.Timestamp;
            sender.OriginationsCount--;

            if (contractBaker != null)
            {
                contractBaker.LastLevel = block.Level;
                contractBaker.LastTimestamp = block.Timestamp;
                if (contractBaker != sender)
                    contractBaker.OriginationsCount--;
            }

            if (initiator != sender && initiator != contractBaker)
            {
                initiator.OriginationsCount--;
            }

            Cache.Chain.Get().OriginationOpsCount--;
            #endregion

            Db.OriginationOps.Remove(origination);
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual int GetConsumedGas(JsonElement result)
        {
            return result.OptionalInt32("consumed_gas") ?? 0;
        }

        protected virtual IMicheline GetCode(JsonElement content)
        {
            return content.TryGetProperty("script", out var script)
                ? Micheline.FromJson(script.Required("code"))!
                // WTF: Before Proto5 some contracts had no code nor storage
                : Micheline.FromBytes(MichelsonScript.ManagerTzBytes);
        }

        protected virtual IMicheline GetStorage(JsonElement content)
        {
            return content.TryGetProperty("script", out var script)
                ? Micheline.FromJson(script.Required("storage"))!
                // WTF: Different nodes return different manager prop name.
                : new MichelineString(content.OptionalString("managerPubkey") ?? content.RequiredString("manager_pubkey"));
        }

        protected async Task<MichelineArray> ExpandCode(L1Contract contract, IMicheline code)
        {
            if (code is not MichelineArray array)
            {
                var constants = await Constants.Find(Db, contract.ChainId, [code]);
                if (constants.Count > 0)
                {
                    contract.Tags |= L1ContractTags.Constants;
                    foreach (var constant in constants)
                    {
                        Db.TryAttach(constant);
                        constant.Refs++;
                    }
                    var dict = constants.ToDictionary(x => x.Address!, x => Micheline.FromBytes(x.Value!));
                    array = Constants.Expand(code, dict) as MichelineArray
                        ?? throw new Exception("Contract code should be an array or constant");
                }
                else
                {
                    throw new Exception("Contract code should be an array or constant");
                }
            }
            return array;
        }

        protected async Task ProcessScript(L1OriginationOperation origination, L1Contract contract, MichelineArray code, IMicheline storageValue)
        {
            #region expand top-level constants
            var constants = await Constants.Find(Db, contract.ChainId, code);
            if (constants.Count > 0)
            {
                var depth = 0;
                while (code.Any(x => x is MichelinePrim prim && prim.Prim == PrimType.constant) && depth++ <= 10_000)
                {
                    for (int i = 0; i < code.Count; i++)
                    {
                        if (code[i] is MichelinePrim prim && prim.Prim == PrimType.constant)
                        {
                            code[i] = Micheline.FromBytes(constants.First(x => x.Address == (prim.Args![0] as MichelineString)!.Value).Value!);
                        }
                    }
                }
            }
            #endregion

            var micheParameter = code.First(x => x is MichelinePrim p && p.Prim == PrimType.parameter);
            var micheStorage = code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage);
            var micheCode = code.First(x => x is MichelinePrim p && p.Prim == PrimType.code);
            var micheViews = code.Where(x => x is MichelinePrim p && p.Prim == PrimType.view);

            #region process constants
            if (constants.Count > 0)
            {
                contract.Tags |= L1ContractTags.Constants;
                foreach (var constant in constants)
                {
                    Db.TryAttach(constant);
                    constant.Refs++;
                }
                var dict = constants.ToDictionary(x => x.Address!, x => Micheline.FromBytes(x.Value!));
                micheParameter = Constants.Expand(micheParameter, dict);
                micheStorage = Constants.Expand(micheStorage, dict);
                foreach (var view in micheViews.OfType<MichelinePrim>())
                {
                    view.Args![1] = Constants.Expand(view.Args[1], dict);
                    view.Args[2] = Constants.Expand(view.Args[2], dict);
                }
            }
            #endregion

            var script = new MichelsonScript
            {
                Id = Cache.Chain.NextScriptId(),
                ChainId = contract.ChainId,
                Level = origination.Level,
                ContractId = contract.Id,
                OriginationId = origination.Id,
                ParameterSchema = micheParameter.ToBytes(),
                StorageSchema = micheStorage.ToBytes(),
                CodeSchema = micheCode.ToBytes(),
                Views = micheViews.Any()
                    ? [..micheViews.Select(x => x.ToBytes())]
                    : null,
                Current = true
            };

            var viewsBytes = script.Views?
                .OrderBy(x => x, BytesComparer.Instance)
                .SelectMany(x => x)
                .ToArray()
                ?? [];
            var typeSchema = script.ParameterSchema.Concat(script.StorageSchema).Concat(viewsBytes);
            var fullSchema = typeSchema.Concat(script.CodeSchema);
            contract.TypeHash = script.TypeHash = MichelsonScript.GetHash(typeSchema);
            origination.ContractCodeHash = contract.CodeHash = script.CodeHash = MichelsonScript.GetHash(fullSchema);

            if ((storageValue.Type == MichelineType.String || storageValue.Type == MichelineType.Bytes) &&
                code.ToBytes().IsEqual(MichelsonScript.ManagerTzBytes))
            {
                contract.Kind = L1ContractKind.DelegatorContract;
            }
            else
            {
                if (script.Schema.IsFA1())
                {
                    if (script.Schema.IsFA12())
                        contract.Tags |= L1ContractTags.FA12;

                    contract.Tags |= L1ContractTags.FA1;
                    contract.Kind = L1ContractKind.Asset;
                }
                if (script.Schema.IsFA2())
                {
                    contract.Tags |= L1ContractTags.FA2;
                    contract.Kind = L1ContractKind.Asset;
                }
            }

            if (BigMapDiffs != null)
            {
                var ind = 0;
                var ptrs = BigMapDiffs.Where(x => x.Action <= BigMapDiffAction.Copy && x.Ptr >= 0).Select(x => x.Ptr).ToList();
                var view = script.Schema.Storage.Schema.ToTreeView(storageValue);

                foreach (var bigmap in view.Nodes().Where(x => x.Schema.Prim == PrimType.big_map))
                    storageValue = storageValue.Replace(bigmap.Value, new MichelineInt(ptrs[^++ind]));
            }

            var storage = new Storage
            {
                Id = Cache.Chain.NextStorageId(),
                ChainId = contract.ChainId,
                Level = origination.Level,
                ContractId = contract.Id,
                OriginationId = origination.Id,
                RawValue = script.Schema.OptimizeStorage(storageValue, false).ToBytes(),
                JsonValue = Regexes.RestrictedUnicode().Replace(script.Schema.HumanizeStorage(storageValue), Regexes.NullEscapeString),
                Current = true
            };

            Db.Scripts.Add(script);
            Cache.Schemas.Add(contract, script.Schema);

            Db.Storages.Add(storage);
            Cache.Storages.Add(contract, storage);

            origination.ScriptId = script.Id;
            origination.StorageId = storage.Id;
        }

        protected async Task RevertScript(L1OriginationOperation origination, L1Contract contract)
        {
            #region process constants
            if (contract.Tags.HasFlag(L1ContractTags.Constants))
            {
                var script = await Db.Scripts
                    .AsNoTracking()
                    .OfType<MichelsonScript>()
                    .Where(x => x.ContractId == contract.Id && x.Current)
                    .Select(x => new { x.ParameterSchema, x.StorageSchema, x.CodeSchema, x.Views })
                    .FirstAsync();

                var code = new MichelineArray
                {
                    Micheline.FromBytes(script.ParameterSchema),
                    Micheline.FromBytes(script.StorageSchema),
                    Micheline.FromBytes(script.CodeSchema)
                };
                if (script.Views != null)
                    foreach (var bytes in script.Views)
                        code.Add(Micheline.FromBytes(bytes));

                // TODO: we're actually missing constants in parameter and storage,
                // as they were expanded, so refs may be reverted inaccurately.
                var constants = await Constants.Find(Db, contract.ChainId, code);
                foreach (var constant in constants)
                {
                    Db.TryAttach(constant);
                    constant.Refs--;
                }
            }
            #endregion

            Db.Scripts.Remove(new MichelsonScript
            {
                Id = origination.ScriptId!.Value,
                ChainId = contract.ChainId,
                ParameterSchema = [],
                StorageSchema = [],
                CodeSchema = [],
                Level = 0,
                ContractId = 0,
            });
            Cache.Schemas.Remove(contract);
            Cache.Chain.ReleaseScriptId();

            if (!Cache.Storages.TryGetCached(contract, out var storage))
            {
                storage = new Storage
                {
                    Id = origination.StorageId!.Value,
                    ChainId = contract.ChainId,
                    RawValue = [],
                    JsonValue = string.Empty,
                    Level = 0,
                    ContractId = 0,
                };
            }
            Db.Storages.Remove(storage);
            Cache.Storages.Remove(contract);
            Cache.Chain.ReleaseStorageId();
        }

        protected virtual IEnumerable<BigMapDiff>? ParseBigMapDiffs(L1OriginationOperation origination, JsonElement result, MichelineArray code, IMicheline storage)
        {
            List<BigMapDiff>? res = null;

            var micheStorage = (code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage) as MichelinePrim)!;
            var schema = new StorageSchema(micheStorage);
            var tree = schema.Schema.ToTreeView(storage);
            var bigmap = tree.Nodes().FirstOrDefault(x => x.Schema.Prim == PrimType.big_map);

            if (bigmap != null)
            {
                res =
                [
                    new AllocDiff { Ptr = origination.ContractId!.Value }
                ];
                if (bigmap.Value is MichelineArray items && items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        var key = (item as MichelinePrim)!.Args![0];
                        var value = (item as MichelinePrim)!.Args![1];
                        res.Add(new UpdateDiff
                        {
                            Ptr = res[0].Ptr,
                            Key = key,
                            Value = value,
                            KeyHash = Hashes.ParseExprHash((bigmap.Schema as BigMapSchema)!.GetKeyHash(key))
                        });
                    }
                }
            }

            return res;
        }
    }
}
