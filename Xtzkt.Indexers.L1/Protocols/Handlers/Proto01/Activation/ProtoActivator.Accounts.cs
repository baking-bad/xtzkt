using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Netezos.Keys;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        protected virtual async Task<List<L1Address>> BootstrapAddresses(L1Protocol protocol, JToken parameters)
        {
            var bootstrapAddresses = parameters["bootstrap_accounts"]?
                .Select(x => (x[0]!.Value<string>()!, x[1]!.Value<long>(), x.Count() > 2 ? x[2]!.Value<string>()! : null))
                .ToList() ?? [];

            var bootstrapContracts = parameters["bootstrap_contracts"]?
                .Select(x =>
                (
                    x["amount"]!.Value<long>(),
                    x["delegate"]?.Value<string>(),
                    x["script"]!["code"]!.ToString(),
                    x["script"]!["storage"]!.ToString(),
                    x["hash"]?.Value<string>()
                ))
                .ToList() ?? [];

            var bootstrapSmartRollups = parameters["bootstrap_smart_rollups"]?
                .Select(x =>
                (
                    x["address"]!.Value<string>()!,
                    x["pvm_kind"]!.Value<string>()!,
                    x["parameters_ty"]!.ToString()
                ))
                .ToList() ?? [];

            var chain = Cache.Chain.Get();
            var addresses = new List<L1Address>(bootstrapAddresses.Count + bootstrapContracts.Count + bootstrapSmartRollups.Count);

            #region allocate null-address
            var nullAddress = new L1User
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = chain.Id,
                Hash = NullAddress.Hash,
                FirstLevel = chain.Level,
                FirstTimestamp = chain.Timestamp,
                LastLevel = chain.Level,
                LastTimestamp = chain.Timestamp,
                Index = 0
            };
            if (nullAddress.Id != NullAddress.Id)
                throw new Exception("Failed to allocate null-address");
            Cache.Addresses.Add(nullAddress);
            Db.Addresses.Add(nullAddress);
            #endregion

            #region bootstrap bakers
            foreach (var (pubKey, balance, _) in bootstrapAddresses.Where(x => x.Item1[0] != 't' && (x.Item3 == null || x.Item3[0] != 't')))
            {
                var address = PubKey.FromBase58(pubKey).Address;
                if (Cache.Addresses.TryGetCached(address, out var acc))
                {
                    Receive(acc, acc as L1Baker, balance);
                    continue;
                }
                var baker = new L1Baker
                {
                    Id = Cache.Chain.NextAddressId(),
                    ChainId = chain.Id,
                    Hash = address,
                    PublicKey = pubKey,
                    FirstLevel = chain.Level,
                    FirstTimestamp = chain.Timestamp,
                    LastLevel = chain.Level,
                    LastTimestamp = chain.Timestamp,
                    ActivationLevel = chain.Level,
                    ActivationTimestamp = chain.Timestamp,
                    DeactivationLevel = GracePeriod.Init(2, protocol),
                    Staked = true,
                    Revealed = true
                };
                Receive(baker, baker, balance);
                Cache.Addresses.Add(baker);
                addresses.Add(baker);

                Cache.Statistics.Current.TotalBakers++;
            }
            #endregion

            #region bootstrap delegated users
            foreach (var (pubKey, balance, delegateTo) in bootstrapAddresses.Where(x => x.Item1[0] != 't' && x.Item3 != null && x.Item3[0] == 't'))
            {
                var baker = Cache.Addresses.GetExistingBaker(delegateTo!);

                var address = PubKey.FromBase58(pubKey).Address;
                if (Cache.Addresses.TryGetCached(address, out var acc))
                {
                    Receive(acc, baker, balance);
                    continue;
                }

                var user = new L1User
                {
                    Id = Cache.Chain.NextAddressId(),
                    ChainId = chain.Id,
                    Hash = address,
                    FirstLevel = chain.Level,
                    FirstTimestamp = chain.Timestamp,
                    LastLevel = chain.Level,
                    LastTimestamp = chain.Timestamp,
                    PublicKey = pubKey,
                    Revealed = true,
                };
                Receive(user, null, balance);

                Delegate(user, baker, chain.Level, chain.Timestamp);

                Cache.Addresses.Add(user);
                addresses.Add(user);
            }
            #endregion

            #region bootstrap users
            foreach (var (pkh, balance, _) in bootstrapAddresses.Where(x => x.Item1[0] == 't'))
            {
                if (Cache.Addresses.TryGetCached(pkh, out var acc))
                {
                    Receive(acc, null, balance);
                    continue;
                }
                var user = new L1User
                {
                    Id = Cache.Chain.NextAddressId(),
                    ChainId = chain.Id,
                    Hash = pkh,
                    FirstLevel = chain.Level,
                    FirstTimestamp = chain.Timestamp,
                    LastLevel = chain.Level,
                    LastTimestamp = chain.Timestamp,
                };
                Receive(user, null, balance);

                Cache.Addresses.Add(user);
                addresses.Add(user);
            }
            #endregion

            #region bootstrap contracts
            if (Proto.Config.Precompiles?.Count > 0)
            {
                foreach (var hash in Proto.Config.Precompiles)
                {
                    var contract = await Proto.Rpc.GetContractAsync(1, hash);
                    var balance = contract.RequiredInt64("balance");
                    var bakerPkh = contract.OptionalString("delegate");
                    var script = contract.Required("script");
                    var codeStr = script.Required("code").GetRawText();
                    var storageStr = script.Required("storage").GetRawText();

                    bootstrapContracts.Add((balance, bakerPkh, codeStr, storageStr, hash));
                }
            }

            var index = 0;
            foreach (var (balance, bakerPkh, codeStr, storageStr, hash) in bootstrapContracts)
            {
                #region contract
                var baker = Cache.Addresses.GetBaker(bakerPkh);
                var creator = nullAddress;
                
                var contract = new L1Contract
                {
                    Id = Cache.Chain.NextAddressId(),
                    ChainId = chain.Id,
                    Hash = hash ?? OriginationNonce.GetContractAddress(index++),
                    FirstLevel = chain.Level,
                    FirstTimestamp = chain.Timestamp,
                    LastLevel = chain.Level,
                    LastTimestamp = chain.Timestamp,
                    CreatorId = creator.Id,
                    Kind = L1ContractKind.SmartContract,
                };
                Receive(contract, null, balance);

                creator.ContractsCount++;

                if (baker != null)
                    Delegate(contract, baker, chain.Level, chain.Timestamp);

                Cache.Addresses.Add(contract);
                addresses.Add(contract);
                #endregion

                #region script
                var code = (Micheline.FromJson(codeStr) as MichelineArray)!;
                var micheParameter = code.First(x => x is MichelinePrim p && p.Prim == PrimType.parameter);
                var micheStorage = code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage);
                var micheCode = code.First(x => x is MichelinePrim p && p.Prim == PrimType.code);
                var micheViews = code.Where(x => x is MichelinePrim p && p.Prim == PrimType.view);
                var script = new MichelsonScript
                {
                    Id = Cache.Chain.NextScriptId(),
                    ChainId = contract.ChainId,
                    Level = 1,
                    ContractId = contract.Id,
                    ParameterSchema = micheParameter.ToBytes(),
                    StorageSchema = micheStorage.ToBytes(),
                    CodeSchema = micheCode.ToBytes(),
                    Views = micheViews.Any()
                        ? [..micheViews.Select(x => x.ToBytes())]
                        : null,
                    Current = true
                };

                var viewsBytes = script.Views?
                    .OrderBy(x => x, new BytesComparer())
                    .SelectMany(x => x)
                    .ToArray()
                    ?? [];
                var typeSchema = script.ParameterSchema.Concat(script.StorageSchema).Concat(viewsBytes);
                var fullSchema = typeSchema.Concat(script.CodeSchema);
                contract.TypeHash = script.TypeHash = MichelsonScript.GetHash(typeSchema);
                contract.CodeHash = script.CodeHash = MichelsonScript.GetHash(fullSchema);

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

                Db.Scripts.Add(script);
                Cache.Schemas.Add(contract, script.Schema);
                #endregion

                #region storage
                var storageValue = Micheline.FromJson(storageStr)!;
                var storage = new Storage
                {
                    Id = Cache.Chain.NextStorageId(),
                    ChainId = contract.ChainId,
                    Level = 1,
                    ContractId = contract.Id,
                    RawValue = script.Schema.OptimizeStorage(storageValue, false).ToBytes(),
                    JsonValue = Regexes.RestrictedUnicode().Replace(script.Schema.HumanizeStorage(storageValue), Regexes.NullEscapeString),
                    Current = true
                };

                Db.Storages.Add(storage);
                Cache.Storages.Add(contract, storage);
                #endregion

            }
            #endregion

            #region bootstrap smart rollups
            foreach (var (address, pvmKind, parameterType) in bootstrapSmartRollups)
            {
                var genesisInfo = await Proto.Rpc.GetSmartRollupGenesisInfo(1, address);

                var creator = nullAddress;
                var rollup = new L1SmartRollup()
                {
                    Id = Cache.Chain.NextAddressId(),
                    ChainId = chain.Id,
                    FirstLevel = chain.Level,
                    FirstTimestamp = chain.Timestamp,
                    LastLevel = chain.Level,
                    LastTimestamp = chain.Timestamp,
                    Hash = address,
                    CreatorId = creator.Id,
                    PvmKind = pvmKind switch
                    {
                        "arith" => PvmKind.Arith,
                        "wasm_2_0_0" => PvmKind.Wasm,
                        _ => throw new NotImplementedException()
                    },
                    ParameterSchema = Micheline.FromJson(parameterType)!.ToBytes(),
                    GenesisCommitment = genesisInfo.RequiredString("commitment_hash"),
                    LastCommitment = genesisInfo.RequiredString("commitment_hash"),
                    InboxLevel = genesisInfo.RequiredInt32("level"),
                    TotalStakers = 0,
                    ActiveStakers = 0,
                    ExecutedCommitments = 0,
                    CementedCommitments = 0,
                    PendingCommitments = 0,
                    RefutedCommitments = 0,
                    OrphanCommitments = 0,
                    SmartRollupBonds = 0
                };
                Cache.Addresses.Add(rollup);
                addresses.Add(rollup);

                creator.SmartRollupsCount++;
            }
            #endregion

            Db.Addresses.AddRange(addresses);

            #region migration ops
            var block = Cache.Blocks.Current();

            block.Operations |= L1Operations.Migration;

            foreach (var address in addresses)
            {
                var migration = new MichelsonMigrationOperation
                {
                    Id = Cache.Chain.NextOperationId(),
                    ChainId = block.ChainId,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                    AddressId = address.Id,
                    Kind = MigrationKind.Bootstrap,
                    BalanceChange = address.Balance,
                };

                if (address is L1Contract contract)
                {
                    var script = (Db.ChangeTracker.Entries()
                        .First(x => x.Entity is MichelsonScript s && s.ContractId == contract.Id).Entity as MichelsonScript)!;
                    var storage = await Cache.Storages.GetAsync(contract);
                    
                    script.MigrationId = migration.Id;
                    storage.MigrationId = migration.Id;

                    migration.ScriptId = script.Id;
                    migration.StorageId = storage.Id;
                }

                Db.MigrationOps.Add(migration);
                Context.MigrationOps.Add(migration);
                address.MigrationsCount++;
            }

            chain.MigrationOpsCount += addresses.Count;
            #endregion

            #region statistics
            Cache.Statistics.Current.TotalBootstrapped = addresses.Sum(x => x.Balance);
            #endregion

            return addresses;
        }

        async Task ClearAddresses()
        {
            var chain = Cache.Chain.Get();

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "Addresses" WHERE "ChainId" = {0};
                DELETE FROM "MigrationOps" WHERE "ChainId" = {0};
                DELETE FROM "Scripts" WHERE "ChainId" = {0};
                DELETE FROM "Storages" WHERE "ChainId" = {0};
                """, chain.Id);

            await Cache.Addresses.ResetAsync();
            Cache.Schemas.Reset();
            Cache.Storages.Reset();

            Cache.Chain.ReleaseOperationId(chain.MigrationOpsCount);
            chain.AddressCounter = 0;
            chain.MigrationOpsCount = 0;
            chain.ScriptCounter = 0;
            chain.StorageCounter = 0;
        }
    }
}
