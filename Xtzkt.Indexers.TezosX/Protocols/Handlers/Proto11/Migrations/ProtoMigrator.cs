using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override async Task ApplyMigrations(XChain state, MetaBlock block)
    {
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.MichelsonGateway, ProtoActivator.MichelsonGatewayAbi, state);

        var nullAddress = await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress;
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.RescueGuard, ProtoActivator.RescueGuardAbi, nullAddress, state);

        if (state.MichelsonActivationLevel < Context.Block.Level)
        {
            await UpgradeMichelsonGateway(state);
            await new RescueClones(Proto).Bind(state);
        }
    }

    protected override async Task RevertMigrations(XChain state)
    {
        if (state.MichelsonActivationLevel < Context.Block.Level)
        {
            await new RescueClones(Proto).Unbind(state);
            await DowngradeMichelsonGateway(state);
        }

        await Helpers.RemoveEvmPrecompile(EvmRuntime.RescueGuard, state);
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.MichelsonGateway, state);
    }

    async Task UpgradeMichelsonGateway(XChain state)
    {
        var gateway = (XMichelsonContract)await Cache.Addresses.GetExistingAsync(MichelsonRuntime.EvmGateway);

        var rawContract = await Proto.MichelsonRpc.GetContractAsync(Context.Block.Level, gateway.Hash);
        var code = (MichelineArray)rawContract.Required("script").RequiredMicheline("code");
        var micheParameter = code.First(x => x is MichelinePrim p && p.Prim == PrimType.parameter);
        var micheStorage = code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage);
        var micheCode = code.First(x => x is MichelinePrim p && p.Prim == PrimType.code);
        var micheViews = code.Where(x => x is MichelinePrim p && p.Prim == PrimType.view);

        var migration = new MichelsonMigrationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = Context.Block.ChainId,
            Level = Context.Block.Level,
            Timestamp = Context.Block.Timestamp,
            AddressId = gateway.Id,
            Kind = MigrationKind.CodeChange,
        };

        var script = new MichelsonScript
        {
            Id = Cache.Chain.NextScriptId(),
            ChainId = gateway.ChainId,
            Level = Context.Block.Level,
            ContractId = gateway.Id,
            MigrationId = migration.Id,
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
        var fullSchema = script.ParameterSchema.Concat(script.StorageSchema).Concat(viewsBytes).Concat(script.CodeSchema);
        script.CodeHash = MichelsonScript.GetHash(fullSchema);

        var storageValue = rawContract.Required("script").RequiredMicheline("storage");
        var storage = new Storage
        {
            Id = Cache.Chain.NextStorageId(),
            ChainId = gateway.ChainId,
            Level = Context.Block.Level,
            ContractId = gateway.Id,
            MigrationId = migration.Id,
            RawValue = script.Schema.OptimizeStorage(storageValue, false).ToBytes(),
            JsonValue = Regexes.RestrictedUnicode().Replace(script.Schema.HumanizeStorage(storageValue), Regexes.NullEscapeString),
            Current = true
        };

        var oldStorage = await Cache.Storages.GetAsync(gateway);
        if (script.CodeHash == gateway.CodeHash && storage.RawValue.IsEqual(oldStorage.RawValue))
        {
            Cache.Chain.ReleaseStorageId();
            Cache.Chain.ReleaseScriptId();
            Cache.Chain.ReleaseOperationId();
            return;
        }

        var oldScript = await Db.Scripts
            .OfType<MichelsonScript>()
            .FirstAsync(x => x.ContractId == gateway.Id && x.Current);
        oldScript.Current = false;

        Db.TryAttach(oldStorage);
        oldStorage.Current = false;

        migration.ScriptId = script.Id;
        migration.StorageId = storage.Id;

        Db.TryAttach(gateway);
        gateway.CodeHash = script.CodeHash;
        gateway.MigrationsCount++;
        gateway.LastLevel = Context.Block.Level;
        gateway.LastTimestamp = Context.Block.Timestamp;

        state.MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);

        Db.Scripts.Add(script);
        Cache.Schemas.Add(gateway, script.Schema);

        Db.Storages.Add(storage);
        Cache.Storages.Add(gateway, storage);
    }

    async Task DowngradeMichelsonGateway(XChain state)
    {
        var gateway = (XMichelsonContract)await Cache.Addresses.GetExistingAsync(MichelsonRuntime.EvmGateway);

        var (lo, hi) = IdLayout.Id64Range(Context.Block.ChainId, Context.Block.Id);
        var migration = await Db.MigrationOps
            .OfType<MichelsonMigrationOperation>()
            .FirstOrDefaultAsync(x => x.Id >= lo && x.Id <= hi && x.AddressId == gateway.Id && x.Kind == MigrationKind.CodeChange);

        // the script didn't change
        if (migration == null)
            return;

        var scripts = await Db.Scripts
            .OfType<MichelsonScript>()
            .Where(x => x.ContractId == gateway.Id && x.Id <= migration.ScriptId)
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync();

        var storages = await Db.Storages
            .Where(x => x.ContractId == gateway.Id && x.Id <= migration.StorageId)
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync();

        scripts[1].Current = true;
        storages[1].Current = true;

        Db.TryAttach(gateway);
        gateway.CodeHash = scripts[1].CodeHash;
        gateway.MigrationsCount--;
        gateway.LastLevel = Context.Block.Level;
        gateway.LastTimestamp = Context.Block.Timestamp;

        Db.Scripts.Remove(scripts[0]);
        Db.Storages.Remove(storages[0]);
        Db.MigrationOps.Remove(migration);

        Cache.Schemas.Remove(gateway);
        Cache.Storages.Remove(gateway);

        state.MigrationOpsCount--;
        Cache.Chain.ReleaseStorageId();
        Cache.Chain.ReleaseScriptId();
        Cache.Chain.ReleaseOperationId();
    }
}
