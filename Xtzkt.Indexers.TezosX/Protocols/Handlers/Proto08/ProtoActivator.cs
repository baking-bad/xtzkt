using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

public class ProtoActivator(ProtocolHandler proto) : ProtocolCommit(proto), IActivator
{
    protected readonly IMichelsonRpc MichelsonRpc = proto.MichelsonRpc;

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
            HardEvmBlockGasLimit = 2L << 50,
            HardEvmOperationGasLimit = 2L << 50,
            DaFeePerByte = 4,
            DaFeePerByte18 = new BigInteger(4_000_000_000_000),
        };

        Context.Block.ProtocolId = protocol.Id;
        Context.Protocol = protocol;

        Cache.Protocols.Add(protocol);
        Db.Protocols.Add(protocol);
        #endregion

        #region precompiles
        var nullAddress = Helpers.BootstrapEvmPrecompile(EvmRuntime.NullAddress, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/InternalForwarderAbi.json", null, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.XtzBridge, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/XtzBridgeAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.FaBridge, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/FaBridgeAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.Outbox, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/OutboxAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.TicketTable, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/TicketTableAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.GlobalCounter, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/GlobalCounterAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.SequencerUpdater, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/SequencerUpdaterAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.MichelsonGateway, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/MichelsonGatewayAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.AliasForwarder, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/AliasForwarderAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.VerifyTezosSignature, "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/VerifyTezosSignatureAbi.json", nullAddress, state);
        #endregion

        #region bootstrap
        await Helpers.BootstrapEvmUser("0x6ce4d79d4e77402e1ef3417fdda433aa744c6e1c", state);
        await Helpers.BootstrapEvmUser("0xb53dc01974176e5dff2298c5a94343c2585e3c54", state);
        await Helpers.BootstrapEvmUser("0x9b49c988b5817be31dfb00f7a5a4671772dcce2b", state);
        #endregion
    }

    public async Task DeactivateEvmContext(XChain state)
    {
        #region bootstrap
        await Helpers.RemoveEvmUser("0x6ce4d79d4e77402e1ef3417fdda433aa744c6e1c", state);
        await Helpers.RemoveEvmUser("0xb53dc01974176e5dff2298c5a94343c2585e3c54", state);
        await Helpers.RemoveEvmUser("0x9b49c988b5817be31dfb00f7a5a4671772dcce2b", state);
        #endregion

        #region precompiles
        await Helpers.RemoveEvmPrecompile(EvmRuntime.VerifyTezosSignature, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.AliasForwarder, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.MichelsonGateway, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.SequencerUpdater, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.GlobalCounter, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.TicketTable, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.Outbox, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.FaBridge, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.XtzBridge, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.NullAddress, state);
        #endregion

        #region protocol
        await Db.Protocols
            .Where(x => x.ChainId == state.Id && x.Hash == state.Kernel)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseProtocolId();
        await Cache.Protocols.ResetAsync();
        #endregion
    }

    public async Task ActivateMichelsonContext(XChain state, MetaBlock block)
    {
        #region state
        var rawBlock = block.MichelsonBlock
            ?? throw new Exception("Missing Michelson block at activation level");

        state.MichelsonChainId = rawBlock.RequiredString("chain_id");
        state.MichelsonProtocol = rawBlock.Required("metadata").RequiredString("next_protocol");
        state.MichelsonBlock = rawBlock.Required("header").RequiredString("predecessor");
        #endregion

        #region protocol
        var constants = await MichelsonRpc.GetConstantsAsync(Context.Block.Level);
        Db.TryAttach(Context.Protocol);
        Context.Protocol.MichelsonHash = state.MichelsonProtocol;
        Context.Protocol.OriginationSize = constants.OptionalInt32("origination_size") ?? 257;
        Context.Protocol.ByteCost = 1; // TODO: uncomment when fixed: constants.OptionalInt32("cost_per_byte") ?? 250;
        Context.Protocol.HardMichelsonBlockGasLimit = constants.OptionalInt32("hard_gas_limit_per_block") ?? 3_000_000;
        Context.Protocol.HardMichelsonOperationGasLimit = constants.OptionalInt32("hard_gas_limit_per_operation") ?? 3_000_000;
        Context.Protocol.HardMichelsonOperationStorageLimit = constants.OptionalInt32("hard_storage_limit_per_operation") ?? 60_000;
        #endregion

        #region null-address
        var nullAddress = new XMichelsonUser
        {
            Id = Cache.Chain.NextAddressId(),
            ChainId = state.Id,
            Hash = MichelsonRuntime.NullAddress,
            FirstLevel = Context.Block.Level,
            FirstTimestamp = Context.Block.Timestamp,
            LastLevel = Context.Block.Level,
            LastTimestamp = Context.Block.Timestamp,
        };

        Context.Block.Events |= XBlockEvents.NewAddresses;

        Cache.Addresses.Add(nullAddress);
        Db.Addresses.Add(nullAddress);
        #endregion

        #region gateway
        var gateway = new XMichelsonContract
        {
            Id = Cache.Chain.NextAddressId(),
            ChainId = state.Id,
            Hash = MichelsonRuntime.EvmGateway,
            FirstLevel = Context.Block.Level,
            FirstTimestamp = Context.Block.Timestamp,
            LastLevel = Context.Block.Level,
            LastTimestamp = Context.Block.Timestamp,
            CreatorId = nullAddress.Id,
            Kind = XContractKind.SmartContract,
        };

        nullAddress.ContractsCount++;

        Context.Block.Events |= XBlockEvents.NewAddresses;

        Cache.Addresses.Add(gateway);
        Db.Addresses.Add(gateway);

        var gatewayInfo = await Proto.MichelsonRpc.GetContractAsync(Context.Block.Level, gateway.Hash);
        var code = (MichelineArray)gatewayInfo.Required("script").RequiredMicheline("code");
        var micheParameter = code.First(x => x is MichelinePrim p && p.Prim == PrimType.parameter);
        var micheStorage = code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage);
        var micheCode = code.First(x => x is MichelinePrim p && p.Prim == PrimType.code);
        var micheViews = code.Where(x => x is MichelinePrim p && p.Prim == PrimType.view);
        var script = new MichelsonScript
        {
            Id = Cache.Chain.NextScriptId(),
            ChainId = gateway.ChainId,
            Level = 1,
            ContractId = gateway.Id,
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
        gateway.TypeHash = script.TypeHash = MichelsonScript.GetHash(typeSchema);
        gateway.CodeHash = script.CodeHash = MichelsonScript.GetHash(fullSchema);

        Db.Scripts.Add(script);
        Cache.Schemas.Add(gateway, script.Schema);

        var storageValue = gatewayInfo.Required("script").RequiredMicheline("storage");
        var storage = new Storage
        {
            Id = Cache.Chain.NextStorageId(),
            ChainId = gateway.ChainId,
            Level = 1,
            ContractId = gateway.Id,
            RawValue = script.Schema.OptimizeStorage(storageValue, false).ToBytes(),
            JsonValue = Regexes.RestrictedUnicode().Replace(script.Schema.HumanizeStorage(storageValue), Regexes.NullEscapeString),
            Current = true
        };

        Db.Storages.Add(storage);
        Cache.Storages.Add(gateway, storage);

        var gatewayMigration = new MichelsonMigrationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = Context.Block.ChainId,
            Level = Context.Block.Level,
            Timestamp = Context.Block.Timestamp,
            AddressId = gateway.Id,
            Kind = MigrationKind.Bootstrap,
            ScriptId = script.Id,
            StorageId = storage.Id,
        };

        script.MigrationId = gatewayMigration.Id;
        storage.MigrationId = gatewayMigration.Id;

        gateway.MigrationsCount++;
        state.MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(gatewayMigration);
        Db.MigrationOps.Add(gatewayMigration);
        #endregion

        // TODO: remove bootstrap after tezos x release
        #region bootstrap
        var addresses = new List<XAddress>();
        var addressHashes = await MichelsonRpc.GetContractsAsync(Context.Block.Level);
        foreach (var address in addressHashes.EnumerateArray().Select(x => x.RequiredString()))
        {
            if (address.StartsWith("KT1"))
                throw new NotImplementedException("Smart contracts bootstrap is not implemented");

            var rawContract = await MichelsonRpc.GetContractAsync(Context.Block.Level, address);
            var rawKey = await MichelsonRpc.GetContractManagerKeyAsync(Context.Block.Level, address);

            #region address
            var user = new XMichelsonUser
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = state.Id,
                Hash = address,
                FirstLevel = Context.Block.Level,
                FirstTimestamp = Context.Block.Timestamp,
                LastLevel = Context.Block.Level,
                LastTimestamp = Context.Block.Timestamp,
                Balance = rawContract.RequiredInt64("balance"),
                Counter = rawContract.RequiredInt32("counter"),
                PublicKey = rawKey.OptionalString(),
                Revealed = rawKey.OptionalString() != null
            };

            Context.Block.Events |= XBlockEvents.NewAddresses;

            Cache.Addresses.Add(user);
            Db.Addresses.Add(user);
            #endregion

            #region migration
            var migration = new MichelsonMigrationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = state.Id,
                Level = Context.Block.Level,
                Timestamp = Context.Block.Timestamp,
                AddressId = user.Id,
                Kind = MigrationKind.Bootstrap,
                BalanceChange = user.Balance,
            };

            user.MigrationsCount++;
            state.MigrationOpsCount++;

            Context.Block.Operations |= XOperations.Migration;
            Context.Statistics.TotalBootstrapped += new BigInteger(migration.BalanceChange) * M12;

            Context.MigrationOps.Add(migration);
            Db.MigrationOps.Add(migration);
            #endregion
        }
        #endregion
    }

    public async Task DeactivateMichelsonContext(XChain state)
    {
        #region state
        state.MichelsonChainId = null;
        state.MichelsonProtocol = null;
        state.MichelsonBlock = null;
        #endregion

        #region protocol
        var protocol = await Cache.Protocols.GetAsync(state.Kernel);
        Db.TryAttach(protocol);
        protocol.MichelsonHash = null;
        protocol.OriginationSize = 0;
        protocol.ByteCost = 0;
        protocol.HardMichelsonBlockGasLimit = 0;
        protocol.HardMichelsonOperationGasLimit = 0;
        protocol.HardMichelsonOperationStorageLimit = 0;
        #endregion

        #region null-address + gateway + bootstrap
        var migrations = await Db.MigrationOps
            .OfType<MichelsonMigrationOperation>()
            .Where(x => x.ChainId == state.Id && x.Level == Context.Block.Level && x.Kind == MigrationKind.Bootstrap)
            .ExecuteDeleteAsync();

        var scripts = await Db.Scripts
            .Where(x => x.ChainId == state.Id && x.Runtime == Runtime.Michelson)
            .ExecuteDeleteAsync();

        var storages = await Db.Storages
            .Where(x => x.ChainId == state.Id)
            .ExecuteDeleteAsync();

        var addresses = await Db.Addresses
            .Where(x => x.ChainId == state.Id && x.Runtime == Runtime.Michelson)
            .ExecuteDeleteAsync();

        state.MigrationOpsCount -= migrations;
        Cache.Chain.ReleaseOperationId(migrations);
        Cache.Chain.ReleaseScriptId(scripts);
        Cache.Chain.ReleaseStorageId(storages);
        Cache.Chain.ReleaseAddressId(addresses);
        Cache.Addresses.Reset();
        Cache.Schemas.Reset();
        Cache.Storages.Reset();
        #endregion
    }
}
