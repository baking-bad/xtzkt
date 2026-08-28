using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override async Task ApplyMigrations(XChain state, MetaBlock block)
    {
        #region precompiles
        var nullAddress = (await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress)!;
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.NullAddress, [], "Protocols/Handlers/Proto02/Runtimes/Evm/Precompiles/NullAbi.json", state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.FaBridge, [], "Protocols/Handlers/Proto02/Runtimes/Evm/Precompiles/FaBridgeAbi.json", nullAddress, state);
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

    protected override Task RevertMigrations(XChain state)
    {
        throw new NotImplementedException();
    }
}
