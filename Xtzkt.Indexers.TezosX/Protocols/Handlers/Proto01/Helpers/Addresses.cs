using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;

// Customized helpers due lack of call traces in the genesis era.
public partial class ProtoHelpers
{
    // The michelson runtime doesn't exist in this era, so there's nothing to bind aliases to.
    protected override Task BindAliases(XEvmAddress address) => Task.CompletedTask;
    protected override Task UnbindAliases(XEvmAddress address) => Task.CompletedTask;
    protected override Task BindAliases(XMichelsonAddress address) => Task.CompletedTask;
    protected override Task UnbindAliases(XMichelsonAddress address) => Task.CompletedTask;

    public override async Task<XEvmAddress> GetOrCreateXEvmAddress(string hash)
    {
        if (await Cache.Addresses.GetOrDefaultAsync(hash) is XEvmAddress address)
            return address;

        if (await GetCode(hash) is byte[] code)
            return await BootstrapXEvmContract(hash, code);

        return await CreateXEvmUser(hash);
    }

    public override async Task<XEvmContract> GetOrCreateXEvmContract(string hash)
    {
        var address = await Cache.Addresses.GetOrDefaultAsync(hash);
        if (address is XEvmContract contract)
            return contract;

        if (address is not null and not XEvmUser)
            throw new InvalidOperationException($"Cannot interpret {address.Type} as XEvmContract");

        if (await GetCode(hash) is not byte[] code)
            throw new Exception($"Address {hash} has no code in the node, but emitted a log");

        return await BootstrapXEvmContract(hash, code);
    }

    async Task<XEvmContract> BootstrapXEvmContract(string hash, byte[] code)
    {
        // the actual creator is unobservable without traces, therefore we use NullAddress
        var creator = (await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress)!;
        Db.TryAttach(creator);

        // creates the contract, or upgrades the already known user in place
        var contract = await CreateXEvmContract(hash, creator);
        Db.TryAttach(contract);

        // solidity appends a cbor blob to the end of the runtime code
        SolidityMetadata.TryRead(code, out var metadata);

        var codeHash = EvmScript.GetHash(code);
        var script = new EvmScript
        {
            Id = Cache.Chain.NextScriptId(),
            ChainId = contract.ChainId,
            ContractId = contract.Id,
            Level = Context.Block.Level,
            Code = code,
            CodeHash = codeHash,
            TypeHash = codeHash,
            Current = true,
            SolidityMetadataBzzr0 = metadata?.Bzzr0,
            SolidityMetadataBzzr1 = metadata?.Bzzr1,
            SolidityMetadataIpfs = metadata?.IpfsCid,
            SolidityMetadataSolc = metadata?.SolcVersion,
            SolidityMetadataExperimental = metadata?.Experimental,
        };
        Cache.Abi.Add(contract, null);
        Db.Scripts.Add(script);

        var migration = new EvmMigrationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = Context.Block.ChainId,
            Level = Context.Block.Level,
            Timestamp = Context.Block.Timestamp,
            AddressId = contract.Id,
            Kind = MigrationKind.Bootstrap,
            ScriptId = script.Id,
        };

        script.MigrationId = migration.Id;

        contract.CodeHash = codeHash;
        contract.TypeHash = codeHash;
        contract.MigrationsCount++;
        contract.LastLevel = migration.Level;
        contract.LastTimestamp = migration.Timestamp;

        Cache.Chain.Get().MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);

        return contract;
    }

    async Task<byte[]?> GetCode(string hash)
    {
        if (Context.Block.Level == 0)
            return null;

        var code = (await EvmRpc.GetCode(hash, Context.Block.Level)).RequiredHexBytes();
        return code.Length != 0 ? code : null;
    }
}
