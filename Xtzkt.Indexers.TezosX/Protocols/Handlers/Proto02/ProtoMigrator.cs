using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

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
        var nullAddress = (await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress)!;
        await UpgradeEvmPrecompile(EvmRuntime.NullAddress, "Protocols/Handlers/Proto02/Runtimes/Evm/Precompiles/NullAbi.json", state);
        BootstrapEvmPrecompile(EvmRuntime.FaBridge, "Protocols/Handlers/Proto02/Runtimes/Evm/Precompiles/FaBridgeAbi.json", nullAddress, state);
        #endregion

        #region amend empty traces
        var hashes = JsonSerializer.Deserialize<string[]>(
            File.ReadAllBytes("Protocols/Handlers/Proto02/Migrations/addresses.json"))!;

        var addresses = await Db.Addresses
            .AsNoTracking()
            .OfType<XEvmAddress>()
            .Where(x => x.ChainId == state.Id)
            .ToDictionaryAsync(x => x.Hash);

        const int chunkSize = 256;
        for (int i = 0; i < hashes.Length; i += chunkSize)
        {
            var chunk = hashes[i..Math.Min(i + chunkSize, hashes.Length)];

            var balancesTask = Proto.EvmRpc.GetBalance(chunk, Context.Block.Level - 1);
            var noncesTask = Proto.EvmRpc.GetNonce(chunk, Context.Block.Level - 1);
            var codesTask = Proto.EvmRpc.GetCode(chunk, Context.Block.Level - 1);
            await Task.WhenAll(balancesTask, noncesTask, codesTask);

            var balances = balancesTask.Result.Select(x => x.RequiredHexBigInteger()).ToArray();
            var nonces = noncesTask.Result.Select(x => x.RequiredHexInt32()).ToArray();
            var codes = codesTask.Result.Select(x => x.RequiredHexBytes()).ToArray();

            for (int j = 0; j < chunk.Length; j++)
            {
                var address = Cache.Addresses.TryGetCached(chunk[j], out var cachedAddress)
                    ? (cachedAddress as XEvmAddress)!
                    : addresses.TryGetValue(chunk[j], out var existingAddress)
                        ? existingAddress
                        : await Helpers.CreateXEvmUser(chunk[j]);
                
                var balance = balances[j];
                var nonce = nonces[j];
                var code = codes[j];

                if (address.Id == nullAddress.Id)
                {
                    // null address nonce was incremented
                    nonce++;
                }

                if (address.Balance != balance || address.Counter != nonce - 1 || address is XEvmUser && code.Length != 0)
                {
                    var migration = new EvmMigrationOperation
                    {
                        Id = Cache.Chain.NextOperationId(),
                        ChainId = state.Id,
                        Level = Context.Block.Level,
                        Timestamp = Context.Block.Timestamp,
                        AddressId = address.Id,
                        Kind = MigrationKind.AmendAddress,
                        BalanceChange = balance - address.Balance,
                        NonceChange = nonce - 1 - address.Counter,
                    };

                    Db.TryAttach(address);
                    address.Balance = balance;
                    address.Counter = nonce - 1;
                    address.MigrationsCount++;
                    address.LastLevel = Context.Block.Level;
                    address.LastTimestamp = Context.Block.Timestamp;

                    if (address is XEvmUser user && code.Length != 0)
                    {
                        var contract = Helpers.UpgradeToXEvmContract(user, nullAddress);
                        contract.CodeHash = EvmScript.GetHash(code);
                        contract.TypeHash = EvmScript.GetHash(code);
                        contract.Counter = nonce - 1;

                        SolidityMetadata.TryRead(code, out var metadata);

                        var script = new EvmScript
                        {
                            Id = Cache.Chain.NextScriptId(),
                            ChainId = state.Id,
                            ContractId = contract.Id,
                            Level = Context.Block.Level,
                            Code = code,
                            CodeHash = contract.CodeHash,
                            TypeHash = contract.TypeHash,
                            Current = true,
                            MigrationId = migration.Id,
                            SolidityMetadataBzzr0 = metadata?.Bzzr0,
                            SolidityMetadataBzzr1 = metadata?.Bzzr1,
                            SolidityMetadataIpfs = metadata?.IpfsCid,
                            SolidityMetadataSolc = metadata?.SolcVersion,
                            SolidityMetadataExperimental = metadata?.Experimental,
                        };
                        Cache.Abi.Add(contract, null);
                        Db.Scripts.Add(script);

                        migration.ScriptId = script.Id;
                    }

                    state.MigrationOpsCount++;

                    Context.Statistics.TotalBurned -= migration.BalanceChange;
                    if (address.Hash == EvmRuntime.NullAddress || address.Hash == EvmRuntime.DeadAddress)
                        Context.Statistics.TotalBanished += migration.BalanceChange;

                    Context.Block.Operations |= XOperations.Migration;

                    Context.MigrationOps.Add(migration);
                    Db.MigrationOps.Add(migration);
                }
            }
        }
        #endregion

        #region burn xtz bridge balance
        var xtzBridge = (await Cache.Addresses.GetExistingAsync(EvmRuntime.XtzBridge) as XEvmContract)!;
        if (xtzBridge.Balance != BigInteger.Zero)
        {
            var migration = new EvmMigrationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = state.Id,
                Level = Context.Block.Level,
                Timestamp = Context.Block.Timestamp,
                AddressId = xtzBridge.Id,
                Kind = MigrationKind.BurnBalance,
                BalanceChange = -xtzBridge.Balance,
            };

            Db.TryAttach(xtzBridge);
            xtzBridge.Balance = BigInteger.Zero;
            xtzBridge.MigrationsCount++;
            xtzBridge.LastLevel = Context.Block.Level;
            xtzBridge.LastTimestamp = Context.Block.Timestamp;

            state.MigrationOpsCount++;

            Context.Statistics.TotalBurned += -migration.BalanceChange;

            Context.Block.Operations |= XOperations.Migration;

            Context.MigrationOps.Add(migration);
            Db.MigrationOps.Add(migration);
        }
        #endregion
    }

    public async Task RevertContext(XChain state)
    {
        throw new NotImplementedException();
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

        Db.TryAttach(creator ?? contract);
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

        Db.TryAttach(contract);
        contract.MigrationsCount++;

        state.MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);

        return contract;
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
