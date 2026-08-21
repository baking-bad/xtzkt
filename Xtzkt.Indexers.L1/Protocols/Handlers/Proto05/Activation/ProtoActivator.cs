using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto05
{
    class ProtoActivator(ProtocolHandler proto) : Proto04.ProtoActivator(proto)
    {
        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.BallotQuorumMin = parameters["quorum_min"]?.Value<int>() ?? 2000;
            protocol.BallotQuorumMax = parameters["quorum_max"]?.Value<int>() ?? 7000;
            protocol.ProposalQuorum = parameters["min_proposal_quorum"]?.Value<int>() ?? 500;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.BallotQuorumMin = 2000;
            protocol.BallotQuorumMax = 7000;
            protocol.ProposalQuorum = 500;
        }

        // Airdrop
        // Proposal invoice
        // Code change

        protected override async Task MigrateContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();
            Db.TryAttach(block);

            var statistics = Cache.Statistics.Current;
            Db.TryAttach(statistics);

            #region airdrop
            var managers = File.ReadAllLines("./Protocols/Handlers/Proto05/Activation/airdropped.contracts");

            if (state.ChainId == "NetXdQprcVkpaWU") // mainnet
                await Cache.Addresses.LoadAsync(managers, state.Level, state.Timestamp);
            else
                await Cache.Addresses.Preload(managers);

            foreach (var managerAddress in managers)
            {
                if (Cache.Addresses.TryGetCached(managerAddress, out var manager))
                {
                    Db.TryAttach(manager);

                    Receive(manager, 1);
                    manager.Counter = state.ManagerCounter;
                    manager.MigrationsCount++;
                    manager.LastLevel = block.Level;
                    manager.LastTimestamp = block.Timestamp;

                    block.Operations |= L1Operations.Migration;

                    var airdropMigration = new MichelsonMigrationOperation
                    {
                        Id = Cache.Chain.NextOperationId(),
                        ChainId = block.ChainId,
                        Level = state.Level,
                        Timestamp = state.Timestamp,
                        AddressId = manager.Id,
                        Kind = MigrationKind.AirDrop,
                        BalanceChange = 1
                    };
                    Db.MigrationOps.Add(airdropMigration);
                    Context.MigrationOps.Add(airdropMigration);

                    state.MigrationOpsCount++;
                    statistics.TotalCreated += airdropMigration.BalanceChange;
                }
            }
            #endregion

            #region invoice
            var address = (await Cache.Addresses.GetAsync("KT1DUfaMfTRZZkvZAYQT5b3byXnvqoAykc43", Context.Block))!;
            Db.TryAttach(address);
            Receive(address, 500_000_000);
            address.MigrationsCount++;
            address.LastLevel = block.Level;
            address.LastTimestamp = block.Timestamp;

            block.Operations |= L1Operations.Migration;

            var invoiceMigration = new MichelsonMigrationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                AddressId = address.Id,
                Kind = MigrationKind.ProposalInvoice,
                BalanceChange = 500_000_000
            };
            Db.MigrationOps.Add(invoiceMigration);
            Context.MigrationOps.Add(invoiceMigration);

            state.MigrationOpsCount++;
            statistics.TotalCreated += 500_000_000;
            #endregion

            #region scripts
            var contracts = await Db.Addresses
                .OfType<L1Contract>()
                .Where(x => x.ChainId == state.Id && x.Type == AddressType.L1Contract)
                .ToListAsync(); // ~27k
            var scripts = await Db.Scripts
                .OfType<MichelsonScript>()
                .Where(x => x.ChainId == state.Id && x.Current)
                .ToDictionaryAsync(x => x.ContractId);
            var storages = await Db.Storages.Where(x => x.ChainId == state.Id && x.Current).ToDictionaryAsync(x => x.ContractId);
            var originations = await Db.OriginationOps
                .OfType<L1OriginationOperation>()
                .Where(x => x.ChainId == state.Id && x.ContractId != null)
                .ToDictionaryAsync(x => x.ContractId!.Value);

            Cache.Schemas.Reset();
            Cache.Storages.Reset();

            foreach (var contract in contracts)
            {
                Cache.Addresses.Update(contract);

                if (contract.Kind == L1ContractKind.DelegatorContract)
                {
                    var script = scripts[contract.Id];
                    script.Level = block.Level;
                    script.OriginationId = null;

                    var storage = storages[contract.Id];
                    storage.Level = block.Level;
                    storage.OriginationId = null;

                    var origination = originations[contract.Id];
                    origination.ScriptId = null;
                    origination.StorageId = null;

                    var migration = new MichelsonMigrationOperation
                    {
                        Id = Cache.Chain.NextOperationId(),
                        ChainId = block.ChainId,
                        Level = block.Level,
                        Timestamp = block.Timestamp,
                        AddressId = contract.Id,
                        Kind = MigrationKind.CodeChange,
                        ScriptId = script.Id,
                        StorageId = storage.Id
                    };

                    script.MigrationId = migration.Id;
                    storage.MigrationId = migration.Id;

                    contract.MigrationsCount++;
                    contract.LastLevel = block.Level;
                    contract.LastTimestamp = block.Timestamp;

                    state.MigrationOpsCount++;

                    Db.MigrationOps.Add(migration);
                    Context.MigrationOps.Add(migration);
                }
                else
                {
                    var script = scripts[contract.Id];
                    var storage = storages[contract.Id];

                    var rawContract = await Proto.Rpc.GetContractAsync(block.Level, contract.Hash);

                    var code = (Micheline.FromJson(rawContract.Required("script").Required("code")) as MichelineArray)!;
                    var micheParameter = code.First(x => x is MichelinePrim p && p.Prim == PrimType.parameter).ToBytes();
                    var micheStorage = code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage).ToBytes();
                    var micheCode = code.First(x => x is MichelinePrim p && p.Prim == PrimType.code).ToBytes();
                    var micheViews = code.Where(x => x is MichelinePrim p && p.Prim == PrimType.view);

                    var newSchema = new Netezos.Contracts.ContractScript(code);
                    var newStorageValue = Micheline.FromJson(rawContract.Required("script").Required("storage"))!;
                    var newRawStorageValue = newSchema.OptimizeStorage(newStorageValue, false).ToBytes();

                    if (script.ParameterSchema.IsEqual(micheParameter) &&
                        script.StorageSchema.IsEqual(micheStorage) &&
                        script.CodeSchema.IsEqual(micheCode) &&
                        storage.RawValue.IsEqual(newRawStorageValue))
                        continue;

                    script.Current = false;
                    storage.Current = false;

                    var migration = new MichelsonMigrationOperation
                    {
                        Id = Cache.Chain.NextOperationId(),
                        ChainId = block.ChainId,
                        Level = block.Level,
                        Timestamp = block.Timestamp,
                        AddressId = contract.Id,
                        Kind = MigrationKind.CodeChange
                    };
                    var newScript = new MichelsonScript
                    {
                        Id = Cache.Chain.NextScriptId(),
                        ChainId = contract.ChainId,
                        Level = migration.Level,
                        ContractId = contract.Id,
                        MigrationId = migration.Id,
                        ParameterSchema = micheParameter,
                        StorageSchema = micheStorage,
                        CodeSchema = micheCode,
                        Views = micheViews.Any()
                            ? micheViews.Select(x => x.ToBytes()).ToArray()
                            : null,
                        Current = true
                    };
                    var newStorage = new Storage
                    {
                        Id = Cache.Chain.NextStorageId(),
                        ChainId = contract.ChainId,
                        Level = migration.Level,
                        ContractId = contract.Id,
                        MigrationId = migration.Id,
                        RawValue = newRawStorageValue,
                        JsonValue = Regexes.RestrictedUnicode().Replace(newScript.Schema.HumanizeStorage(newStorageValue), Regexes.NullEscapeString),
                        Current = true
                    };

                    var viewsBytes = newScript.Views?
                        .OrderBy(x => x, BytesComparer.Instance)
                        .SelectMany(x => x)
                        .ToArray()
                        ?? [];
                    var typeSchema = newScript.ParameterSchema.Concat(newScript.StorageSchema).Concat(viewsBytes);
                    var fullSchema = typeSchema.Concat(newScript.CodeSchema);
                    contract.TypeHash = newScript.TypeHash = MichelsonScript.GetHash(typeSchema);
                    contract.CodeHash = newScript.CodeHash = MichelsonScript.GetHash(fullSchema);

                    migration.ScriptId = newScript.Id;
                    migration.StorageId = newStorage.Id;

                    contract.MigrationsCount++;
                    contract.LastLevel = block.Level;
                    contract.LastTimestamp = block.Timestamp;

                    state.MigrationOpsCount++;

                    Db.MigrationOps.Add(migration);
                    Context.MigrationOps.Add(migration);

                    Db.Scripts.Add(newScript);
                    Cache.Schemas.Add(contract, newScript.Schema);

                    Db.Storages.Add(newStorage);
                    Cache.Storages.Add(contract, newStorage);

                    var tree = script.Schema.Storage.Schema.ToTreeView(Micheline.FromBytes(storage.RawValue));
                    var bigmap = tree.Nodes().FirstOrDefault(x => x.Schema.Prim == PrimType.big_map);
                    if (bigmap != null)
                    {
                        var newTree = newScript.Schema.Storage.Schema.ToTreeView(Micheline.FromBytes(newStorage.RawValue));
                        var newBigmap = newTree.Nodes().FirstOrDefault(x => x.Schema.Prim == PrimType.big_map);
                        if (newBigmap?.Value is not MichelineInt micheInt)
                            throw new Exception("Expected micheline int");
                        var newPtr = (int)micheInt.Value;

                        if (newBigmap.Path != bigmap.Path)
                            await Db.Database.ExecuteSqlRawAsync("""
                                UPDATE "BigMaps"
                                SET "StoragePath" = {0}
                                WHERE "ChainId" = {1}
                                AND "Ptr" = {2}
                                """, newBigmap.Path, contract.ChainId, contract.Id);

                        await Db.Database.ExecuteSqlRawAsync("""
                            UPDATE "BigMaps"
                            SET "Ptr" = {0}
                            WHERE "ChainId" = {1}
                            AND "Ptr" = {2};
                            """, newPtr, contract.ChainId, contract.Id);

                        foreach (var prevStorage in await Db.Storages.Where(x => x.ContractId == contract.Id).ToListAsync())
                        {
                            var prevValue = Micheline.FromBytes(prevStorage.RawValue);
                            var prevTree = script.Schema.Storage.Schema.ToTreeView(prevValue);
                            var prevBigmap = prevTree.Nodes().First(x => x.Schema.Prim == PrimType.big_map);
                            (prevBigmap.Value as MichelineInt)!.Value = newPtr;

                            prevStorage.RawValue = prevValue.ToBytes();
                            prevStorage.JsonValue = Regexes.RestrictedUnicode().Replace(script.Schema.HumanizeStorage(prevValue), Regexes.NullEscapeString);
                        }
                    }
                }
            }
            #endregion
        }

        protected override async Task RevertContext(L1Chain state)
        {
            #region airdrop
            var airDrops = await Db.MigrationOps
                .AsNoTracking()
                .OfType<MichelsonMigrationOperation>()
                .Where(x => x.ChainId == state.Id && x.Level == state.Level && x.Kind == MigrationKind.AirDrop)
                .ToListAsync();

            foreach (var airDrop in airDrops)
            {
                var address = await Cache.Addresses.GetAsync(airDrop.AddressId);
                Db.TryAttach(address);

                RevertReceive(address, 1);
                address.MigrationsCount--;
            }

            Db.MigrationOps.RemoveRange(airDrops);
            Cache.Chain.ReleaseOperationId(airDrops.Count);

            state.MigrationOpsCount -= airDrops.Count;
            #endregion

            #region invoice
            var invoice = await Db.MigrationOps
                .AsNoTracking()
                .OfType<MichelsonMigrationOperation>()
                .FirstAsync(x => x.ChainId == state.Id && x.Level == state.Level && x.Kind == MigrationKind.ProposalInvoice);

            var invoiceAddress = await Cache.Addresses.GetAsync(invoice.AddressId);
            Db.TryAttach(invoiceAddress);

            RevertReceive(invoiceAddress, 500_000_000);
            invoiceAddress.MigrationsCount--;

            Db.MigrationOps.Remove(invoice);
            Cache.Chain.ReleaseOperationId();

            state.MigrationOpsCount--;
            #endregion

            #region scripts
            var contracts = await Db.Addresses
                .OfType<L1Contract>()
                .Where(x => x.ChainId == state.Id && x.Type == AddressType.L1Contract)
                .ToDictionaryAsync(x => x.Id); // ~27k
            var scripts = await Db.Scripts
                .OfType<MichelsonScript>()
                .Where(x => x.ChainId == state.Id && x.Current)
                .ToDictionaryAsync(x => x.ContractId);
            var storages = await Db.Storages.Where(x => x.ChainId == state.Id && x.Current).ToDictionaryAsync(x => x.ContractId);
            var originations = await Db.OriginationOps
                .OfType<L1OriginationOperation>()
                .Where(x => x.ChainId == state.Id && x.ContractId != null)
                .ToDictionaryAsync(x => x.ContractId!.Value);

            var codeChanges = await Db.MigrationOps
                .OfType<MichelsonMigrationOperation>()
                .Where(x => x.ChainId == state.Id && x.Level == state.Level && x.Kind == MigrationKind.CodeChange)
                .ToListAsync();

            Cache.Schemas.Reset();
            Cache.Storages.Reset();

            foreach (var change in codeChanges)
            {
                var contract = contracts[change.AddressId];
                Cache.Addresses.Update(contract);

                if (contract.Kind == L1ContractKind.DelegatorContract)
                {
                    var origination = originations[contract.Id];

                    var script = scripts[contract.Id];
                    script.Level = origination.Level;
                    script.OriginationId = origination.Id;

                    var storage = storages[contract.Id];
                    storage.Level = origination.Level;
                    storage.OriginationId = origination.Id;

                    origination.ScriptId = script.Id;
                    origination.StorageId = storage.Id;

                    script.MigrationId = null;
                    storage.MigrationId = null;

                    contract.MigrationsCount--;
                    contract.LastLevel = state.Level;
                    contract.LastTimestamp = state.Timestamp;
                }
                else
                {
                    var script = scripts[contract.Id];
                    var storage = storages[contract.Id];

                    var oldScript = await Db.Scripts
                        .OfType<MichelsonScript>()
                        .Where(x => x.ContractId == contract.Id && x.Id < script.Id)
                        .OrderByDescending(x => x.Id)
                        .FirstAsync();

                    var oldStorage = await Db.Storages
                        .Where(x => x.ContractId == contract.Id && x.Id < storage.Id)
                        .OrderByDescending(x => x.Id)
                        .FirstAsync();

                    var tree = script.Schema.Storage.Schema.ToTreeView(Micheline.FromBytes(storage.RawValue));
                    var bigmap = tree.Nodes().FirstOrDefault(x => x.Schema.Prim == PrimType.big_map);
                    if (bigmap != null)
                    {
                        var oldTree = oldScript.Schema.Storage.Schema.ToTreeView(Micheline.FromBytes(oldStorage.RawValue));
                        var oldBigmap = oldTree.Nodes().First(x => x.Schema.Prim == PrimType.big_map);

                        if (bigmap.Value is not MichelineInt mi)
                            throw new Exception("Expected micheline int");
                        var newPtr = (int)mi.Value;

                        if (oldBigmap.Path != bigmap.Path)
                            await Db.Database.ExecuteSqlRawAsync("""
                                UPDATE "BigMaps"
                                SET "StoragePath" = {0}
                                WHERE "ChainId" = {1}
                                AND "Ptr" = {2}
                                """, oldBigmap.Path, state.Id, newPtr);

                        await Db.Database.ExecuteSqlRawAsync("""
                            UPDATE "BigMaps"
                            SET "Ptr" = {0}
                            WHERE "ChainId" = {1}
                            AND "Ptr" = {2};
                            """, contract.Id, state.Id, newPtr);

                        foreach (var prevStorage in await Db.Storages.Where(x => x.ContractId == contract.Id && x.Level < change.Level).ToListAsync())
                        {
                            var prevValue = Micheline.FromBytes(prevStorage.RawValue);
                            var prevTree = oldScript.Schema.Storage.Schema.ToTreeView(prevValue);
                            var prevBigmap = prevTree.Nodes().First(x => x.Schema.Prim == PrimType.big_map);
                            (prevBigmap.Value as MichelineInt)!.Value = contract.Id;

                            prevStorage.RawValue = prevValue.ToBytes();
                            prevStorage.JsonValue = Regexes.RestrictedUnicode().Replace(oldScript.Schema.HumanizeStorage(prevValue), Regexes.NullEscapeString);
                        }
                    }

                    oldScript.Current = true;
                    Cache.Schemas.Add(contract, oldScript.Schema);

                    oldStorage.Current = true;
                    Cache.Storages.Add(contract, oldStorage);

                    Db.Scripts.Remove(script);
                    Cache.Chain.ReleaseScriptId();

                    Db.Storages.Remove(storage);
                    Cache.Chain.ReleaseStorageId();

                    contract.TypeHash = oldScript.TypeHash;
                    contract.CodeHash = oldScript.CodeHash;
                    contract.MigrationsCount--;
                }
            }

            Db.MigrationOps.RemoveRange(codeChanges);
            Cache.Chain.ReleaseOperationId(codeChanges.Count);
            state.MigrationOpsCount -= codeChanges.Count;
            #endregion
        }
    }
}
