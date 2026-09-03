using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
{
    #region evm
    public new const string NullAddressAbi = "Protocols/Handlers/Proto10/Runtimes/Evm/Precompiles/NullAddressAbi.json";
    public new const string XtzBridgeAbi = "Protocols/Handlers/Proto10/Runtimes/Evm/Precompiles/XtzBridgeAbi.json";
    public const string MichelsonGatewayAbi = "Protocols/Handlers/Proto10/Runtimes/Evm/Precompiles/MichelsonGatewayAbi.json";
    public const string AliasForwarderAbi = "Protocols/Handlers/Proto10/Runtimes/Evm/Precompiles/AliasForwarderAbi.json";
    public const string VerifyTezosSignatureAbi = "Protocols/Handlers/Proto10/Runtimes/Evm/Precompiles/VerifyTezosSignatureAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress,            NullAddressAbi),
        (EvmRuntime.XtzBridge,              XtzBridgeAbi),
        (EvmRuntime.FaBridge,               Proto08.ProtoActivator.FaBridgeAbi),
        (EvmRuntime.Outbox,                 Proto06.ProtoActivator.OutboxAbi),
        (EvmRuntime.TicketTable,            Proto07.ProtoActivator.TicketTableAbi),
        (EvmRuntime.GlobalCounter,          Proto06.ProtoActivator.GlobalCounterAbi),
        (EvmRuntime.SequencerUpdater,       Proto06.ProtoActivator.SequencerUpdaterAbi),
        (EvmRuntime.MichelsonGateway,       MichelsonGatewayAbi),
        (EvmRuntime.AliasForwarder,         AliasForwarderAbi),
        (EvmRuntime.VerifyTezosSignature,   VerifyTezosSignatureAbi),
    ];
    #endregion

    #region michelson
    public override async Task ActivateMichelsonContext(XChain state, MetaBlock block)
    {
        #region state
        var rawBlock = block.MichelsonBlock
            ?? throw new Exception("Missing Michelson block at activation level");

        state.MichelsonChainId = rawBlock.RequiredString("chain_id");
        state.MichelsonProtocol = rawBlock.Required("metadata").RequiredString("next_protocol");
        state.MichelsonBlock = rawBlock.Required("header").RequiredString("predecessor");
        #endregion

        #region protocol
        var constants = await Proto.MichelsonRpc.GetConstantsAsync(Context.Block.Level);
        Db.TryAttach(Context.Protocol);
        Context.Protocol.MichelsonHash = state.MichelsonProtocol;
        Context.Protocol.OriginationSize = constants.OptionalInt32("origination_size") ?? 257;
        Context.Protocol.ByteCost = constants.OptionalInt32("cost_per_byte") ?? 1;
        Context.Protocol.HardMichelsonBlockGasLimit = constants.OptionalInt32("hard_gas_limit_per_block") ?? 660_000;
        Context.Protocol.HardMichelsonOperationGasLimit = constants.OptionalInt32("hard_gas_limit_per_operation") ?? 660_000;
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
    }

    public override async Task DeactivateMichelsonContext(XChain state)
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
    #endregion
}
