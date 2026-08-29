using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

partial class OriginationCommit
{
    public virtual async Task<XEvmOriginationOperation> ApplyEvm(string hash, JsonElement tx, JsonElement receipt, JsonElement trace, bool isDelayedOp)
    {
        #region init
        var block = Context.Block;
        var senderAddress = tx.RequiredString("from");
        var sender = await Helpers.GetOrCreateXEvmUser(senderAddress);

        var effectiveGasPrice = receipt.RequiredHexBigInteger("effectiveGasPrice");
        var gasUsed = receipt.RequiredHexInt32("gasUsed");
        var ownGasUsed = gasUsed - SubcallsGasUsed(trace);
        var status = receipt.RequiredEvmOpStatus("status");

        var daFee = Helpers.GetDaFee(tx, isDelayedOp);
        var gasFee = Helpers.GetGasFee(effectiveGasPrice, gasUsed, daFee);

        var op = new XEvmOriginationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = hash,
            OpType = receipt.RequiredEvmOpType("type"),
            OpCode = trace.RequiredEvmOpCode("type"),
            GasPrice = tx.OptionalHexBigInteger("gasPrice"),
            MaxFeePerGas = tx.OptionalHexBigInteger("maxFeePerGas"),
            MaxPriorityFeePerGas = tx.OptionalHexBigInteger("maxPriorityFeePerGas"),
            EffectiveGasPrice = effectiveGasPrice,
            SenderId = sender.Id,
            DaFee = daFee,
            GasFee = gasFee,
            Balance = tx.RequiredHexBigInteger("value"),
            Counter = tx.RequiredHexInt32("nonce"),
            GasLimit = GetGasLimit(tx),
            GasUsed = ownGasUsed,
            Status = status
        };
        #endregion

        #region apply operation
        Db.TryAttach(sender);
        PayFee(sender, op.DaFee);
        BurnFee(sender, op.GasFee);
        sender.Counter = op.Counter;
        sender.OriginationsCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        Context.Block.Operations |= XOperations.Origination;

        Cache.Chain.Get().OriginationOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            Spend(sender, op.Balance);

            var contractAddress = trace.RequiredString("to");
            if (await Cache.Addresses.GetOrDefaultAsync(contractAddress) is XEvmContract contract)
            {
                op.ReOriginated = true;

                var prevScript = await Db.Scripts
                    .Where(x => x.ContractId == contract.Id && x.Current)
                    .SingleAsync();
                prevScript.Current = false;

                Db.TryAttach(contract);
            }
            else
            {
                contract = await Helpers.CreateXEvmContract(contractAddress, sender);
            }

            // deployed runtime code
            var code = trace.OptionalHexBytes("output") ?? [];

            // solidity appends a cbor blob to the end of the runtime code
            SolidityMetadata.TryRead(code, out var metadata);

            var script = new EvmScript
            {
                Id = Cache.Chain.NextScriptId(),
                ChainId = op.ChainId,
                ContractId = contract.Id,
                Level = Context.Block.Level,
                Code = code,
                CodeHash = EvmScript.GetHash(code),
                TypeHash = EvmScript.GetHash(code),
                Current = true,
                OriginationId = op.Id,
                SolidityMetadataBzzr0 = metadata?.Bzzr0,
                SolidityMetadataBzzr1 = metadata?.Bzzr1,
                SolidityMetadataIpfs = metadata?.IpfsCid,
                SolidityMetadataSolc = metadata?.SolcVersion,
                SolidityMetadataExperimental = metadata?.Experimental,
            };
            Cache.Abi.Add(contract, null);
            Db.Scripts.Add(script);

            Receive(contract, op.Balance);
            contract.CodeHash = script.CodeHash;
            contract.TypeHash = script.TypeHash;
            contract.OriginationsCount++;
            contract.LastLevel = op.Level;
            contract.LastTimestamp = op.Timestamp;

            op.ContractId = contract.Id;
            op.ContractCodeHash = contract.CodeHash;
            op.ScriptId = script.Id;
        }
        #endregion

        Db.OriginationOps.Add(op);
        Context.OriginationOps.Add(op);

        return op;
    }

    public virtual async Task<XEvmOriginationOperation> ApplyInternalEvm(IParentOperation parent, IParentOperation? cracParent, JsonElement trace, OperationStatus traceStatus, OperationStatus parentTraceStatus)
    {
        #region init
        var block = Context.Block;
        var initiator = Cache.Addresses.GetCached(parent.SenderId);

        var senderAddress = trace.RequiredString("from");
        var sender = await Helpers.GetOrCreateXEvmAddress(senderAddress);
        var senderEip7702Delegate = await GetEip7702Delegate(sender);

        var status = GetEvmTraceStatus(parent.Status, traceStatus);

        var op = new XEvmOriginationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            Hash = parent.Hash,
            OpType = EvmOpType.Trace,
            OpCode = trace.RequiredEvmOpCode("type"),
            InitiatorId = initiator.Id,
            SenderId = sender.Id,
            SenderCodeHash = ((senderEip7702Delegate ?? sender) as XEvmContract)?.CodeHash,
            Balance = trace.RequiredHexBigInteger("value"),
            Counter = parent.Counter,
            GasUsed = trace.RequiredHexInt32("gasUsed") - SubcallsGasUsed(trace),
            Status = status,
            Errors = status != OperationStatus.Applied
                ? trace.OptionalEscapedString("revertReason") ?? trace.OptionalEscapedString("error")
                : null,
            NonceConsumed = GetEvmTraceStatus(parent.Status, parentTraceStatus) == OperationStatus.Applied
                ? true
                : null,
        };
        #endregion

        #region apply operation
        Db.TryAttach(sender);
        sender.OriginationsCount++;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        if (op.NonceConsumed == true)
            sender.Counter++;

        // with eip7702 sender can be initiator
        if (initiator != sender)
            initiator.OriginationsCount++;
        
        cracParent?.GasUsed -= MichelsonRuntime.ConvertGas(op.GasUsed);
        parent.InternalOperations = (parent.InternalOperations ?? 0) + 1;

        Context.Block.Operations |= XOperations.Origination;

        Cache.Chain.Get().OriginationOpsCount++;
        #endregion

        #region apply result
        if (op.Status == OperationStatus.Applied)
        {
            Spend(sender, op.Balance);

            var contractAddress = trace.RequiredString("to");
            if (await Cache.Addresses.GetOrDefaultAsync(contractAddress) is XEvmContract contract)
            {
                op.ReOriginated = true;

                var prevScript = await Db.Scripts
                    .Where(x => x.ContractId == contract.Id && x.Current)
                    .SingleAsync();
                prevScript.Current = false;

                Db.TryAttach(contract);
            }
            else
            {
                contract = await Helpers.CreateXEvmContract(contractAddress, sender);
            }

            // deployed runtime code
            var code = trace.OptionalHexBytes("output") ?? [];

            // solidity appends a cbor blob to the end of the runtime code
            SolidityMetadata.TryRead(code, out var metadata);

            var script = new EvmScript
            {
                Id = Cache.Chain.NextScriptId(),
                ChainId = op.ChainId,
                ContractId = contract.Id,
                Level = Context.Block.Level,
                Code = code,
                CodeHash = EvmScript.GetHash(code),
                TypeHash = EvmScript.GetHash(code),
                Current = true,
                OriginationId = op.Id,
                SolidityMetadataBzzr0 = metadata?.Bzzr0,
                SolidityMetadataBzzr1 = metadata?.Bzzr1,
                SolidityMetadataIpfs = metadata?.IpfsCid,
                SolidityMetadataSolc = metadata?.SolcVersion,
                SolidityMetadataExperimental = metadata?.Experimental,
            };
            Cache.Abi.Add(contract, null);
            Db.Scripts.Add(script);

            Receive(contract, op.Balance);
            contract.CodeHash = script.CodeHash;
            contract.TypeHash = script.TypeHash;
            contract.OriginationsCount++;
            contract.LastLevel = op.Level;
            contract.LastTimestamp = op.Timestamp;

            op.ContractId = contract.Id;
            op.ContractCodeHash = contract.CodeHash;
            op.ScriptId = script.Id;
        }
        #endregion

        Db.OriginationOps.Add(op);
        Context.OriginationOps.Add(op);

        return op;
    }

    public virtual async Task Revert(XEvmOriginationOperation op)
    {
        #region init
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XEvmUser)!;
        var contract = await Cache.Addresses.GetAsync(op.ContractId) as XEvmContract;

        Db.TryAttach(sender);
        Db.TryAttach(contract);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            RevertReceive(contract!, op.Balance);
            contract!.OriginationsCount--;
            contract.LastLevel = op.Level;
            contract.LastTimestamp = op.Timestamp;

            Db.Scripts.Remove(new EvmScript
            {
                Id = op.ScriptId!.Value,
                ChainId = contract.ChainId,
                Code = [],
                Level = 0,
                ContractId = 0,
            });
            Cache.Abi.Remove(contract);
            Cache.Chain.ReleaseScriptId();

            if (op.ReOriginated == true)
            {
                var prevScript = await Db.Scripts
                    .Where(x => x.ContractId == contract.Id && x.Id < op.ScriptId!.Value)
                    .OrderByDescending(x => x.Id)
                    .FirstAsync();
                prevScript.Current = true;

                contract.CodeHash = prevScript.CodeHash;
                contract.TypeHash = prevScript.TypeHash;
            }
            else
            {
                await Helpers.RemoveXEvmContract(contract, sender);
            }

            RevertSpend(sender, op.Balance);
        }
        #endregion

        #region revert operation
        RevertPayFee(sender, op.DaFee);
        RevertBurnFee(sender, op.GasFee);
        sender.Counter = op.Counter - 1;
        sender.OriginationsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;
        if (sender.IsEmpty()) await Helpers.RemoveXEvmUser(sender);

        Cache.Chain.Get().OriginationOpsCount--;
        #endregion

        Db.OriginationOps.Remove(op);
        Cache.Chain.ReleaseOperationId();
    }

    public virtual async Task RevertInternal(XEvmOriginationOperation op)
    {
        #region init
        var initiator = await Cache.Addresses.GetAsync(op.InitiatorId!.Value);
        var sender = (await Cache.Addresses.GetAsync(op.SenderId) as XEvmAddress)!;
        var contract = await Cache.Addresses.GetAsync(op.ContractId) as XEvmContract;

        Db.TryAttach(initiator);
        Db.TryAttach(sender);
        Db.TryAttach(contract);
        #endregion

        #region revert result
        if (op.Status == OperationStatus.Applied)
        {
            RevertReceive(contract!, op.Balance);
            contract!.OriginationsCount--;
            contract.LastLevel = op.Level;
            contract.LastTimestamp = op.Timestamp;

            Db.Scripts.Remove(new EvmScript
            {
                Id = op.ScriptId!.Value,
                ChainId = contract.ChainId,
                Code = [],
                Level = 0,
                ContractId = 0,
            });
            Cache.Abi.Remove(contract);
            Cache.Chain.ReleaseScriptId();

            if (op.ReOriginated == true)
            {
                var prevScript = await Db.Scripts
                    .Where(x => x.ContractId == contract.Id && x.Id < op.ScriptId!.Value)
                    .OrderByDescending(x => x.Id)
                    .FirstAsync();
                prevScript.Current = true;

                contract.CodeHash = prevScript.CodeHash;
                contract.TypeHash = prevScript.TypeHash;
            }
            else
            {
                await Helpers.RemoveXEvmContract(contract, sender);
            }

            RevertSpend(sender, op.Balance);
        }
        #endregion

        #region revert operation
        sender.OriginationsCount--;
        sender.LastLevel = op.Level;
        sender.LastTimestamp = op.Timestamp;

        if (op.NonceConsumed == true)
            sender.Counter--;

        if (sender.IsEmpty()) await Helpers.RemoveXEvmAddress(sender);

        if (initiator != sender)
            initiator.OriginationsCount--;

        Cache.Chain.Get().OriginationOpsCount--;
        #endregion

        Db.OriginationOps.Remove(op);
        Cache.Chain.ReleaseOperationId();
    }
}
