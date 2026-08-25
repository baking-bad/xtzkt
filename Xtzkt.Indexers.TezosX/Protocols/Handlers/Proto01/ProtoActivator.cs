using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

public class ProtoActivator(ProtocolHandler proto) : Proto01Commit(proto), IActivator
{
    public async Task ActivateEvmContext(XChain state)
    {
        #region protocol
        var protocol = new XProtocol
        {
            Id = Cache.Chain.NextProtocolId(),
            ChainId = state.Id,
            Hash = state.Kernel,
            Version = Proto.Version,
            FirstLevel = 0,
            LastLevel = 0,
            MinBlockTimeMs = 500,
            MaxBlockTimeMs = 6000,
            HardEvmBlockGasLimit = 1L << 50,
            HardEvmOperationGasLimit = 30_000_000,
            DaFeePerByte = 4,
            DaFeePerByte18 = new BigInteger(4_000_000_000_000),
        };

        Context.Block.ProtocolId = protocol.Id;
        Context.Protocol = protocol;

        Cache.Protocols.Add(protocol);
        Db.Protocols.Add(protocol);
        #endregion

        #region precompiles
        var nullAddress = BootstrapEvmPrecompile(EvmRuntime.NullAddress, "Protocols/Handlers/Proto01/Runtimes/Evm/Precompiles/NullAbi.json", null, state);
        BootstrapEvmPrecompile(EvmRuntime.XtzBridge, "Protocols/Handlers/Proto01/Runtimes/Evm/Precompiles/XtzBridgeAbi.json", nullAddress, state);
        #endregion
    }

    public async Task DeactivateEvmContext(XChain state)
    {
        #region protocol
        await Db.Protocols
            .Where(x => x.ChainId == state.Id && x.Hash == state.Kernel)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseProtocolId();
        await Cache.Protocols.ResetAsync();
        #endregion

        #region precompiles
        await RemoveEvmPrecompile(EvmRuntime.NullAddress, state);
        await RemoveEvmPrecompile(EvmRuntime.XtzBridge, state);
        #endregion
    }

    XEvmContract BootstrapEvmPrecompile(string address, string abiPath, XAddress? creator, XChain state)
    {
        var id = Cache.Chain.NextAddressId();
        var contract = new XEvmContract
        {
            Id = id,
            ChainId = state.Id,
            Hash = address,
            FirstLevel = Context.Block.Level,
            FirstTimestamp = Context.Block.Timestamp,
            LastLevel = Context.Block.Level,
            LastTimestamp = Context.Block.Timestamp,
            Kind = XContractKind.SmartContract,
            Tags = XEvmContractTags.None,
            CreatorId = creator?.Id ?? id,
            Counter = -1, // contract nonce starts at 1 (EIP161), but for precompiles it's 0
        };

        (creator ?? contract).ContractsCount++;

        Context.Block.Events |= XBlockEvents.NewAddresses;

        Cache.Addresses.Add(contract);
        Db.Addresses.Add(contract);

        var script = new EvmScript
        {
            Id = Cache.Chain.NextScriptId(),
            ChainId = state.Id,
            ContractId = contract.Id,
            Level = Context.Block.Level,
            Code = [],
            Current = true,
            AbiJson = File.ReadAllText(abiPath),
        };

        Cache.Abi.Add(contract, Abi.FromJson(script.AbiJson));
        Db.Scripts.Add(script);

        var migration = new EvmMigrationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = state.Id,
            Level = Context.Block.Level,
            Timestamp = Context.Block.Timestamp,
            AddressId = contract.Id,
            Kind = MigrationKind.Bootstrap,
            ScriptId = script.Id,
        };

        script.MigrationId = migration.Id;

        contract.MigrationsCount++;

        state.MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);

        return contract;
    }

    async Task RemoveEvmPrecompile(string address, XChain state)
    {
        var contract = await Db.Addresses
            .FirstAsync(x => x.ChainId == state.Id && x.Hash == address);

        await Db.Addresses
            .Where(x => x.Id == contract.Id)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseAddressId();
        Cache.Addresses.Reset();

        await Db.Scripts
            .Where(x => x.ContractId == contract.Id)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseScriptId();
        Cache.Abi.Reset();

        await Db.MigrationOps
            .Where(x => x.AddressId == contract.Id)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseOperationId();
        state.MigrationOpsCount--;
    }

    public Task ActivateMichelsonContext(XChain state, IMetaBlock block)
    {
        throw new NotImplementedException();
    }

    public Task DeactivateMichelsonContext(XChain state)
    {
        throw new NotImplementedException();
    }
}
