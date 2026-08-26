using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto03;

public class ProtoMigrator(ProtocolHandler proto) : ProtocolCommit(proto), IMigrator
{
    public async Task MigrateContext(XChain state, MetaBlock block)
    {
        #region protocol
        var prev = await Cache.Protocols.GetAsync(state.Kernel);
        Db.TryAttach(prev);
        prev.LastLevel = state.Level;

        var protocol = new XProtocol
        {
            Id = Cache.Chain.NextProtocolId(),
            ChainId = state.Id,
            Hash = state.KernelUpgrade!,
            Version = Proto.Version,
            FirstLevel = Context.Block.Level,
            LastLevel = -1,
            MichelsonHash = prev.MichelsonHash,
            MinBlockTimeMs = prev.MinBlockTimeMs,
            MaxBlockTimeMs = prev.MaxBlockTimeMs,
            ByteCost = prev.ByteCost,
            OriginationSize = prev.OriginationSize,
            DaFeePerByte = prev.DaFeePerByte,
            DaFeePerByte18 = prev.DaFeePerByte18,
            HardEvmBlockGasLimit = prev.HardEvmBlockGasLimit,
            HardEvmOperationGasLimit = prev.HardEvmOperationGasLimit,
            HardMichelsonBlockGasLimit = prev.HardMichelsonBlockGasLimit,
            HardMichelsonOperationGasLimit = prev.HardMichelsonOperationGasLimit,
            HardMichelsonOperationStorageLimit = prev.HardMichelsonOperationStorageLimit,
        };

        state.Kernel = protocol.Hash;
        state.MichelsonProtocol = protocol.MichelsonHash;
        Context.Block.ProtocolId = protocol.Id;
        Context.Protocol = protocol;

        Cache.Protocols.Add(protocol);
        Db.Protocols.Add(protocol);
        #endregion

        #region precompiles
        // withdrawal events moved from the system address to the emitting precompiles,
        // and the xtz bridge gained the fast withdrawal entrypoint
        await UpgradeEvmPrecompile(EvmRuntime.NullAddress, "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/NullAbi.json", state);
        await UpgradeEvmPrecompile(EvmRuntime.XtzBridge, "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/XtzBridgeAbi.json", state);
        await UpgradeEvmPrecompile(EvmRuntime.FaBridge, "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/FaBridgeAbi.json", state);
        #endregion
    }

    public async Task RevertContext(XChain state)
    {
        throw new NotImplementedException();
    }

    async Task<XEvmContract> UpgradeEvmPrecompile(string address, string abiPath, XChain state)
    {
        var contract = (await Cache.Addresses.GetExistingAsync(address) as XEvmContract)!;

        var oldScript = (await Db.Scripts.FirstAsync(x => x.ContractId == contract.Id && x.Current) as EvmScript)!;
        oldScript.Current = false;

        var newScript = new EvmScript
        {
            Id = Cache.Chain.NextScriptId(),
            ChainId = contract.ChainId,
            ContractId = contract.Id,
            Level = Context.Block.Level,
            Code = oldScript.Code,
            CodeHash = oldScript.CodeHash,
            TypeHash = oldScript.TypeHash,
            SolidityMetadataBzzr0 = oldScript.SolidityMetadataBzzr0,
            SolidityMetadataBzzr1 = oldScript.SolidityMetadataBzzr1,
            SolidityMetadataExperimental = oldScript.SolidityMetadataExperimental,
            SolidityMetadataIpfs = oldScript.SolidityMetadataIpfs,
            SolidityMetadataSolc = oldScript.SolidityMetadataSolc,
            AbiJson = File.ReadAllText(abiPath),
            Current = true,
        };

        Cache.Abi.Add(contract, Abi.FromJson(newScript.AbiJson));
        Db.Scripts.Add(newScript);

        var migration = new EvmMigrationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = contract.ChainId,
            Level = Context.Block.Level,
            Timestamp = Context.Block.Timestamp,
            AddressId = contract.Id,
            Kind = MigrationKind.CodeChange,
            ScriptId = newScript.Id,
        };

        newScript.MigrationId = migration.Id;

        Db.TryAttach(contract);
        contract.MigrationsCount++;

        state.MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);

        return contract;
    }
}
