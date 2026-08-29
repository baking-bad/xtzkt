using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Netezos.Forging;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10
{
    partial class OriginationCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public XMichelsonOriginationOperation Origination { get; private set; } = null!;
        public IEnumerable<BigMapDiff>? BigMapDiffs { get; private set; }
        public XMichelsonContract? Contract { get; private set; }

        public virtual async Task Apply(string hash, JsonElement content, bool isDelayedOp, bool isFirstOp)
        {
            #region init
            var block = Context.Block;
            var senderAddress = content.RequiredString("source");
            var sender = await Helpers.GetOrCreateXMichelsonUser(senderAddress);

            var metadata = content.Required("metadata");
            var result = metadata.Required("operation_result");

            var fee = content.RequiredInt64("fee");
            var counter = content.RequiredInt32("counter");
            var gasLimit = content.RequiredInt32("gas_limit");
            var storageLimit = content.RequiredInt32("storage_limit");
            var balance = content.RequiredInt64("balance");
            var code = Micheline.FromJson(content.Required("script").Required("code"))!;
            var storage = Micheline.FromJson(content.Required("script").Required("storage"))!;
            var status = result.RequiredOpStatus("status");

            var daFee = 0L;
            if (!isDelayedOp)
            {
                var size = LocalForge.ForgeOrigination(new()
                {
                    Source = senderAddress,
                    Balance = balance,
                    Counter = counter,
                    GasLimit = gasLimit,
                    StorageLimit = storageLimit,
                    Fee = fee,
                    Delegate = null,
                    Script = new()
                    {
                        Code = (code as MichelineArray)!, // TODO: update Netezos and remove conversion
                        Storage = storage,
                    },
                }).Length;

                if (isFirstOp)
                    size += 32 + (senderAddress.StartsWith("tz4") ? 96 : 64);

                daFee = size * Context.Protocol.DaFeePerByte;
            }
            var gasFee = fee - daFee;

            var gasRefundUpdate = metadata
                .OptionalArray("balance_updates")?
                .EnumerateArray()
                .FirstOrDefault(x =>
                    x.RequiredString("kind") == "accumulator" &&
                    x.RequiredString("category") == "block fees" &&
                    x.RequiredInt64("change") < 0)
                ?? default;

            var gasRefund = gasRefundUpdate.ValueKind != JsonValueKind.Undefined
                ? -gasRefundUpdate.RequiredInt64("change")
                : 0;

            var paidStorageSizeDiff = result.OptionalInt32("paid_storage_size_diff");
            var (storageFee, allocationFee) = GetStorageFees(result, true, paidStorageSizeDiff);

            var operation = new XMichelsonOriginationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = hash,
                DaFee = daFee,
                GasFee = gasFee,
                GasRefund = gasRefund,
                Counter = counter,
                GasLimit = gasLimit,
                StorageLimit = storageLimit,
                Balance = balance,
                SenderId = sender.Id,
                Status = status,
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
                StorageUsed = paidStorageSizeDiff ?? 0,
                StorageFee = storageFee,
                AllocationFee = allocationFee
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, operation.DaFee);
            BurnFee(sender, operation.GasFee - operation.GasRefund);
            sender.Counter = operation.Counter;
            sender.OriginationsCount++;
            sender.LastLevel = operation.Level;
            sender.LastTimestamp = operation.Timestamp;

            Context.Block.Operations |= XOperations.Origination;

            Cache.Chain.Get().OriginationOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                BurnFee(sender, (operation.StorageFee ?? 0) + (operation.AllocationFee ?? 0));
                Spend(sender, operation.Balance);

                var contractAddress = result.RequiredArray("originated_contracts", 1)[0].RequiredString();
                var contract = await Helpers.CreateXMichelsonContract(contractAddress, sender);

                Receive(contract, operation.Balance);
                contract.OriginationsCount++;
                contract.LastLevel = operation.Level;
                contract.LastTimestamp = operation.Timestamp;

                operation.ContractId = contract.Id;

                var expandedCode = await ExpandCode(contract, code);
                BigMapDiffs = ParseBigMapDiffs(operation, result, expandedCode, storage);
                await ProcessScript(operation, contract, expandedCode, storage);
                Contract = contract;
            }
            #endregion

            Db.OriginationOps.Add(operation);
            Context.OriginationOps.Add(operation);
            Origination = operation;
        }

        public virtual async Task ApplyInternal(IParentOperation parent, IParentOperation? cracParent, JsonElement content)
        {
            #region init
            var block = Context.Block;
            var initiator = Cache.Addresses.GetCached(parent.SenderId);

            var senderAddress = content.RequiredString("source");
            var sender = await Helpers.GetOrCreateXMichelsonAddress(senderAddress);
            
            var result = content.Required("result");

            var consumedMilligas = result.OptionalInt64("consumed_milligas") ?? 0;
            var paidStorageSizeDiff = result.OptionalInt32("paid_storage_size_diff");
            var (storageFee, allocationFee) = GetStorageFees(result, true, paidStorageSizeDiff);

            var operation = new XMichelsonOriginationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                InitiatorId = parent.SenderId,
                Level = parent.Level,
                Timestamp = parent.Timestamp,
                DaFee = 0,
                GasFee = 0,
                GasRefund = 0,
                GasLimit = 0,
                StorageLimit = 0,
                Hash = parent.Hash, 
                Counter = parent.Counter,
                Nonce = content.RequiredInt32("nonce"),
                Balance = content.RequiredInt64("balance"),
                SenderId = sender.Id,
                SenderCodeHash = (sender as XMichelsonContract)?.CodeHash,
                Status = result.RequiredOpStatus("status"),
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)((consumedMilligas + 999) / 1000),
                StorageUsed = paidStorageSizeDiff ?? 0,
                StorageFee = storageFee,
                AllocationFee = allocationFee
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            sender.OriginationsCount++;
            sender.LastLevel = block.Level;
            sender.LastTimestamp = block.Timestamp;

            initiator.OriginationsCount++;

            cracParent?.GasUsed -= EvmRuntime.ConvertGas(consumedMilligas);
            parent.InternalOperations = (parent.InternalOperations ?? 0) + 1;

            block.Operations |= XOperations.Origination;

            Cache.Chain.Get().OriginationOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                if (initiator is XMichelsonAddress _initiator)
                    BurnFee(_initiator, (operation.StorageFee ?? 0) + (operation.AllocationFee ?? 0));
                Spend(sender, operation.Balance);

                var contractAddress = result.RequiredArray("originated_contracts", 1)[0].RequiredString();
                var contract = await Helpers.CreateXMichelsonContract(contractAddress, sender);

                Receive(contract, operation.Balance);
                contract.OriginationsCount++;
                contract.LastLevel = operation.Level;
                contract.LastTimestamp = operation.Timestamp;

                operation.ContractId = contract.Id;

                var code = content.Required("script").RequiredMicheline("code");
                var storage = content.Required("script").RequiredMicheline("storage");
                var expandedCode = await ExpandCode(contract, code);
                BigMapDiffs = ParseBigMapDiffs(operation, result, expandedCode, storage);
                await ProcessScript(operation, contract, expandedCode, storage);
                Contract = contract;
            }
            #endregion

            Db.OriginationOps.Add(operation);
            Context.OriginationOps.Add(operation);
            Origination = operation;
        }

        public virtual async Task Revert(XMichelsonOriginationOperation operation)
        {
            #region init
            var sender = (await Cache.Addresses.GetAsync(operation.SenderId) as XMichelsonUser)!;
            var contract = await Cache.Addresses.GetAsync(operation.ContractId) as XMichelsonContract;

            Db.TryAttach(sender);
            Db.TryAttach(contract);
            #endregion

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
                await RevertScript(operation, contract!);

                RevertReceive(contract!, operation.Balance);
                contract!.OriginationsCount--;
                contract.LastLevel = operation.Level;
                contract.LastTimestamp = operation.Timestamp;

                await Helpers.RemoveXMichelsonContract(contract, sender);

                RevertBurnFee(sender, (operation.StorageFee ?? 0) + (operation.AllocationFee ?? 0));
                RevertSpend(sender, operation.Balance);
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, operation.DaFee);
            RevertBurnFee(sender, operation.GasFee - operation.GasRefund);
            sender.Counter = operation.Counter - 1;
            sender.Revealed = true;
            sender.OriginationsCount--;
            sender.LastLevel = operation.Level;
            sender.LastTimestamp = operation.Timestamp;
            if (sender.IsEmpty()) await Helpers.RemoveXMichelsonUser(sender);

            Cache.Chain.Get().OriginationOpsCount--;
            #endregion

            Db.OriginationOps.Remove(operation);
            Cache.Chain.ReleaseOperationId();
        }

        public virtual async Task RevertInternal(XMichelsonOriginationOperation operation)
        {
            #region init
            var initiator = await Cache.Addresses.GetAsync(operation.InitiatorId!.Value);
            var sender = (await Cache.Addresses.GetAsync(operation.SenderId) as XMichelsonAddress)!;
            var contract = await Cache.Addresses.GetAsync(operation.ContractId) as XMichelsonContract;

            Db.TryAttach(initiator);
            Db.TryAttach(sender);
            Db.TryAttach(contract);
            #endregion

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
                await RevertScript(operation, contract!);

                RevertReceive(contract!, operation.Balance);
                contract!.OriginationsCount--;
                contract.LastLevel = operation.Level;
                contract.LastTimestamp = operation.Timestamp;

                await Helpers.RemoveXMichelsonContract(contract, sender);

                if (initiator is XMichelsonAddress _initiator)
                    RevertBurnFee(_initiator, (operation.StorageFee ?? 0) + (operation.AllocationFee ?? 0));
                RevertSpend(sender, operation.Balance);
            }
            #endregion

            #region revert operation
            initiator.OriginationsCount--;

            sender.OriginationsCount--;
            sender.LastLevel = operation.Level;
            sender.LastTimestamp = operation.Timestamp;
            if (sender.IsEmpty()) await Helpers.RemoveXMichelsonAddress(sender);

            Cache.Chain.Get().OriginationOpsCount--;
            #endregion

            Db.OriginationOps.Remove(operation);
            Cache.Chain.ReleaseOperationId();
        }

        protected async Task<MichelineArray> ExpandCode(XMichelsonContract contract, IMicheline code)
        {
            if (code is not MichelineArray array)
            {
                var constants = await Constants.Find(Db, contract.ChainId, [code]);
                if (constants.Count > 0)
                {
                    contract.Tags |= XMichelsonContractTags.Constants;
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

        protected async Task ProcessScript(XMichelsonOriginationOperation origination, XMichelsonContract contract, MichelineArray code, IMicheline storageValue)
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
                contract.Tags |= XMichelsonContractTags.Constants;
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
                    ? [.. micheViews.Select(x => x.ToBytes())]
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
                //contract.Kind = XContractKind.DelegatorContract;
            }
            else
            {
                if (script.Schema.IsFA1())
                {
                    if (script.Schema.IsFA12())
                        contract.Tags |= XMichelsonContractTags.FA12;

                    contract.Tags |= XMichelsonContractTags.FA1;
                    contract.Kind = XContractKind.Asset;
                }
                if (script.Schema.IsFA2())
                {
                    contract.Tags |= XMichelsonContractTags.FA2;
                    contract.Kind = XContractKind.Asset;
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

        protected async Task RevertScript(XMichelsonOriginationOperation origination, XMichelsonContract contract)
        {
            #region process constants
            if (contract.Tags.HasFlag(XMichelsonContractTags.Constants))
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

        protected virtual IEnumerable<BigMapDiff>? ParseBigMapDiffs(XMichelsonOriginationOperation origination, JsonElement result, MichelineArray code, IMicheline storage)
        {
            return result.TryGetProperty("lazy_storage_diff", out var diffs)
                ? BigMapDiff.ParseLazyStorage(diffs)
                : null;
        }
    }
}
