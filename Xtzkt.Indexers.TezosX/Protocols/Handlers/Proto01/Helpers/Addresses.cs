using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;

partial class ProtoHelpers
{
    #region evm address
    public virtual async Task<XEvmAddress> GetOrCreateXEvmAddress(string hash)
    {
        if (await Cache.Addresses.GetOrDefaultAsync(hash) is XEvmAddress address)
            return address;

        if (await GetCode(hash) is byte[] code)
            return await BootstrapXEvmContract(hash, code);

        return await CreateXEvmUser(hash);
    }

    public async Task RemoveXEvmAddress(XEvmAddress address)
    {
        if (address is XEvmUser user)
        {
            await RemoveXEvmUser(user);
        }
        else if (address is XEvmAlias alias)
        {
            var owner = (await Cache.Addresses.GetAsync(alias.OwnerId) as XMichelsonAddress)!;
            await RemoveXEvmAlias(alias, owner);
        }
        else
        {
            throw new InvalidOperationException($"Cannot remove {address.Type}");
        }
    }
    #endregion

    #region evm user
    public async Task<XEvmUser> GetOrCreateXEvmUser(string hash)
    {
        var address = await Cache.Addresses.GetOrDefaultAsync(hash);
        if (address is XEvmUser user)
            return user;

        if (address is not null)
            throw new InvalidOperationException($"Cannot interpret {address.Type} as XEvmUser");

        return await CreateXEvmUser(hash);
    }

    public async Task<XEvmUser> CreateXEvmUser(string hash)
    {
        var user = new XEvmUser
        {
            Id = Cache.Chain.NextAddressId(),
            ChainId = Context.Block.ChainId,
            Hash = hash,
            FirstLevel = Context.Block.Level,
            FirstTimestamp = Context.Block.Timestamp,
            LastLevel = Context.Block.Level,
            LastTimestamp = Context.Block.Timestamp,
            Counter = -1, // counter keeps the last used nonce, for new address it's -1
        };

        Context.Block.Events |= XBlockEvents.NewAddresses;

        Cache.Addresses.Add(user);
        Db.Addresses.Add(user);

        await BindAliases(user);

        return user;
    }

    public async Task RemoveXEvmUser(XEvmUser user)
    {
        if (user.AliasesCount != 0)
            await UnbindAliases(user);

        Cache.Chain.ReleaseAddressId();
        Cache.Addresses.Remove(user);
        Db.Addresses.Remove(user);
    }
    #endregion

    #region evm alias
    public virtual async Task<XEvmAlias> GetOrCreateXEvmAlias(string hash, XMichelsonAddress owner)
    {
        throw new NotImplementedException();
    }

    public virtual async Task RemoveXEvmAlias(XEvmAlias alias, XMichelsonAddress owner)
    {
        throw new NotImplementedException();
    }

    protected virtual Task BindAliases(XEvmAddress address)
    {
        // there are no aliases in this era
        return Task.CompletedTask;
    }

    protected virtual Task UnbindAliases(XEvmAddress address)
    {
        // there are no aliases in this era
        return Task.CompletedTask;
    }
    #endregion

    #region evm contract
    public virtual async Task<XEvmContract> GetOrCreateXEvmContract(string hash)
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

    public async Task<XEvmContract> CreateXEvmContract(string hash, XEvmAddress creator)
    {
        if (await Cache.Addresses.GetOrDefaultAsync(hash) is XEvmAddress address)
        {
            if (address is XEvmContract)
                throw new InvalidOperationException($"Contract {hash} already exists");

            if (address is not XEvmUser ghost)
                throw new InvalidOperationException($"Cannot upgrade {address.Type} to XEvmContract");

            return UpgradeToXEvmContract(ghost, creator);
        }

        var contract = new XEvmContract
        {
            Id = Cache.Chain.NextAddressId(),
            ChainId = Context.Block.ChainId,
            Hash = hash,
            FirstLevel = Context.Block.Level,
            FirstTimestamp = Context.Block.Timestamp,
            LastLevel = Context.Block.Level,
            LastTimestamp = Context.Block.Timestamp,
            Kind = XContractKind.SmartContract,
            Tags = XEvmContractTags.None,
            CreatorId = creator.Id,
            Counter = 0, // contract nonce starts at 1 (EIP161)
        };

        creator.ContractsCount++;

        Context.Block.Events |= XBlockEvents.NewAddresses;

        Cache.Addresses.Add(contract);
        Db.Addresses.Add(contract);

        await BindAliases(contract);

        return contract;
    }

    public XEvmContract UpgradeToXEvmContract(XEvmUser ghost, XEvmAddress creator)
    {
        var contract = new XEvmContract
        {
            Id = ghost.Id,
            ChainId = ghost.ChainId,
            Hash = ghost.Hash,
            FirstLevel = ghost.FirstLevel,
            FirstTimestamp = ghost.FirstTimestamp,
            LastLevel = ghost.LastLevel,
            LastTimestamp = ghost.LastTimestamp,
            ActiveTicketsCount = ghost.ActiveTicketsCount,
            ActiveTokensCount = ghost.ActiveTokensCount,
            Balance = ghost.Balance,
            BlocksCount = ghost.BlocksCount,
            ContractsCount = ghost.ContractsCount,
            DepositOpsCount = ghost.DepositOpsCount,
            MigrationsCount = ghost.MigrationsCount,
            OriginationsCount = ghost.OriginationsCount,
            TicketBalancesCount = ghost.TicketBalancesCount,
            TicketTransfersCount = ghost.TicketTransfersCount,
            TokenBalancesCount = ghost.TokenBalancesCount,
            TokenTransfersCount = ghost.TokenTransfersCount,
            TransactionsCount = ghost.TransactionsCount,
            Eip7702DelegationCount = ghost.Eip7702DelegationCount,
            LogsCount = ghost.LogsCount,
            AliasesCount = ghost.AliasesCount,
            Kind = XContractKind.SmartContract,
            Tags = XEvmContractTags.None,
            CreatorId = creator.Id,
            Counter = 0, // contract nonce starts at 1 (EIP161)
            CodeHash = 0,
            TypeHash = 0,
            TokensCount = 0,
        };
        Cache.Addresses.Add(contract);
        var isAdded = Db.Entry(ghost).State == EntityState.Added;
        Db.Entry(ghost).State = EntityState.Detached;
        Db.Entry(contract).State = isAdded ? EntityState.Added : EntityState.Modified;

        creator.ContractsCount++;

        return contract;
    }

    public async Task RemoveXEvmContract(XEvmContract contract, XEvmAddress creator)
    {
        if (!contract.IsEmpty())
        {
            DowngradeToXEvmUser(contract, creator);
            return;
        }

        if (contract.AliasesCount != 0)
            await UnbindAliases(contract);

        creator.ContractsCount--;
        creator.LastLevel = Context.Block.Level;
        creator.LastTimestamp = Context.Block.Timestamp;

        Cache.Chain.ReleaseAddressId();
        Cache.Addresses.Remove(contract);
        Db.Addresses.Remove(contract);
    }

    protected void DowngradeToXEvmUser(XEvmContract contract, XEvmAddress creator)
    {
        var user = new XEvmUser
        {
            Id = contract.Id,
            ChainId = contract.ChainId,
            Hash = contract.Hash,
            FirstLevel = contract.FirstLevel,
            FirstTimestamp = contract.FirstTimestamp,
            LastLevel = contract.LastLevel,
            LastTimestamp = contract.LastTimestamp,
            ActiveTicketsCount = contract.ActiveTicketsCount,
            ActiveTokensCount = contract.ActiveTokensCount,
            Balance = contract.Balance,
            BlocksCount = contract.BlocksCount,
            ContractsCount = contract.ContractsCount,
            DepositOpsCount = contract.DepositOpsCount,
            MigrationsCount = contract.MigrationsCount,
            OriginationsCount = contract.OriginationsCount,
            TicketBalancesCount = contract.TicketBalancesCount,
            TicketTransfersCount = contract.TicketTransfersCount,
            TokenBalancesCount = contract.TokenBalancesCount,
            TokenTransfersCount = contract.TokenTransfersCount,
            LogsCount = contract.LogsCount,
            TransactionsCount = contract.TransactionsCount,
            Eip7702DelegationCount = contract.Eip7702DelegationCount,
            AliasesCount = contract.AliasesCount,
            Eip7702DelegateId = null,
            Counter = -1, // counter keeps the last used nonce, for new address it's -1
        };
        Cache.Addresses.Add(user);
        Db.Entry(contract).State = EntityState.Detached;
        Db.Entry(user).State = EntityState.Modified;

        creator.ContractsCount--;
    }
    #endregion

    #region michelson address
    public virtual Task<XMichelsonAddress> GetOrCreateXMichelsonAddress(string hash)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }

    public virtual Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }

    public virtual Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash, XBlock block)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }

    public virtual Task RemoveXMichelsonAddress(XMichelsonAddress address)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }
    #endregion

    #region michelson user
    public virtual Task<XMichelsonUser> GetOrCreateXMichelsonUser(string hash)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }

    public virtual Task RemoveXMichelsonUser(XMichelsonUser user)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }
    #endregion

    #region michelson alias
    public virtual Task<XMichelsonAlias> GetOrCreateXMichelsonAlias(string hash, XEvmAddress owner)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }

    public virtual Task RemoveXMichelsonAlias(XMichelsonAlias alias, XEvmAddress owner)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }
    #endregion

    #region michelson contracts
    public virtual Task<XMichelsonContract> CreateXMichelsonContract(string hash, XMichelsonAddress creator)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }

    public virtual Task RemoveXMichelsonContract(XMichelsonContract contract, XMichelsonAddress creator)
    {
        // there is no Michelson runtime in this era
        throw new NotImplementedException();
    }
    #endregion

    #region migrations
    public async Task<XEvmContract> BootstrapEvmPrecompile(string address, string abiPath, XAddress? creator, XChain state)
    {
        if (await Cache.Addresses.GetOrDefaultAsync(address) != null)
            throw new Exception($"Precompile {address} already exists as a regular address, bootstrap logic must be adjusted.");

        var code = (await EvmRpc.GetCode(address, Context.Block.Level)).RequiredHexBytes();

        var codeHash = EvmScript.GetHash(code);
        SolidityMetadata.TryRead(code, out var metadata);

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
            CodeHash = codeHash,
            TypeHash = codeHash,
        };

        if (creator != null)
        {
            Db.TryAttach(creator);
            creator.ContractsCount++;
            creator.LastLevel = Context.Block.Level;
            creator.LastTimestamp = Context.Block.Timestamp;
        }
        else
        {
            contract.ContractsCount++;
        }

        Context.Block.Events |= XBlockEvents.NewAddresses;

        Cache.Addresses.Add(contract);
        Db.Addresses.Add(contract);

        var script = new EvmScript
        {
            Id = Cache.Chain.NextScriptId(),
            ChainId = state.Id,
            ContractId = contract.Id,
            Level = Context.Block.Level,
            Code = code,
            CodeHash = codeHash,
            TypeHash = codeHash,
            SolidityMetadataBzzr0 = metadata?.Bzzr0,
            SolidityMetadataBzzr1 = metadata?.Bzzr1,
            SolidityMetadataExperimental = metadata?.Experimental,
            SolidityMetadataIpfs = metadata?.IpfsCid,
            SolidityMetadataSolc = metadata?.SolcVersion,
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

    public async Task RemoveEvmPrecompile(string address, XChain state)
    {
        var contract = await Db.Addresses
            .OfType<XEvmContract>()
            .FirstAsync(x => x.ChainId == state.Id && x.Hash == address);

        var creator = await Db.Addresses
            .OfType<XAddress>()
            .FirstAsync(x => x.Id == contract.CreatorId);

        creator.ContractsCount--;
        creator.LastLevel = Context.Block.Level;
        creator.LastTimestamp = Context.Block.Timestamp;

        Cache.Chain.ReleaseAddressId();
        Cache.Addresses.Remove(contract);
        Db.Addresses.Remove(contract);

        Cache.Abi.Remove(contract);
        Cache.Chain.ReleaseScriptId();
        await Db.Scripts
            .Where(x => x.ContractId == contract.Id)
            .ExecuteDeleteAsync();

        state.MigrationOpsCount--;
        Cache.Chain.ReleaseOperationId();
        await Db.MigrationOps
            .Where(x => x.AddressId == contract.Id)
            .ExecuteDeleteAsync();
    }

    public async Task<XEvmContract> UpgradeEvmPrecompile(string address, string abiPath, XChain state)
    {
        var code = (await EvmRpc.GetCode(address, Context.Block.Level)).RequiredHexBytes();

        var contract = (await Cache.Addresses.GetExistingAsync(address) as XEvmContract)!;

        var oldScript = (await Db.Scripts.FirstAsync(x => x.ContractId == contract.Id && x.Current) as EvmScript)!;
        oldScript.Current = false;

        var codeHash = EvmScript.GetHash(code);
        SolidityMetadata.TryRead(code, out var metadata);

        var newScript = new EvmScript
        {
            Id = Cache.Chain.NextScriptId(),
            ChainId = contract.ChainId,
            ContractId = contract.Id,
            Level = Context.Block.Level,
            Code = code,
            CodeHash = codeHash,
            TypeHash = codeHash,
            SolidityMetadataBzzr0 = metadata?.Bzzr0,
            SolidityMetadataBzzr1 = metadata?.Bzzr1,
            SolidityMetadataExperimental = metadata?.Experimental,
            SolidityMetadataIpfs = metadata?.IpfsCid,
            SolidityMetadataSolc = metadata?.SolcVersion,
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
        contract.CodeHash = codeHash;
        contract.TypeHash = codeHash;
        contract.MigrationsCount++;
        contract.LastLevel = Context.Block.Level;
        contract.LastTimestamp = Context.Block.Timestamp;

        state.MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);

        return contract;
    }

    public async Task DowngradeEvmPrecompile(string address, XChain state)
    {
        var contract = (await Cache.Addresses.GetExistingAsync(address) as XEvmContract)!;

        var scripts = await Db.Scripts
            .Where(x => x.ContractId == contract.Id)
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync();

        var newScript = (scripts[0] as EvmScript)!;
        var oldScript = (scripts[1] as EvmScript)!;

        oldScript.Current = true;
        Cache.Abi.Add(contract, oldScript.AbiJson is string abiJson ? Abi.FromJson(abiJson) : null);

        Db.TryAttach(contract);
        contract.CodeHash = oldScript.CodeHash;
        contract.TypeHash = oldScript.TypeHash;
        contract.MigrationsCount--;
        contract.LastLevel = Context.Block.Level;
        contract.LastTimestamp = Context.Block.Timestamp;

        Cache.Chain.ReleaseScriptId();
        Db.Scripts.Remove(newScript);

        state.MigrationOpsCount--;
        Cache.Chain.ReleaseOperationId();
        await Db.MigrationOps
            .Where(x => x.Id == newScript.MigrationId)
            .ExecuteDeleteAsync();
    }

    public async Task BootstrapEvmUser(string address, XChain state)
    {
        var balance = await EvmRpc.GetBalanceEarliest(address);
        if (balance.GetString() == "0x0") return;

        var user = new XEvmUser
        {
            Id = Cache.Chain.NextAddressId(),
            ChainId = state.Id,
            Hash = address,
            FirstLevel = Context.Block.Level,
            FirstTimestamp = Context.Block.Timestamp,
            LastLevel = Context.Block.Level,
            LastTimestamp = Context.Block.Timestamp,
            Balance = balance.RequiredHexBigInteger(),
            Counter = -1, // counter keeps the last used nonce, for new address it's -1
        };

        Context.Block.Events |= XBlockEvents.NewAddresses;

        Cache.Addresses.Add(user);
        Db.Addresses.Add(user);

        var migration = new EvmMigrationOperation
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
        Context.Statistics.TotalBootstrapped += migration.BalanceChange;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);
    }

    public async Task RemoveEvmUser(string address, XChain state)
    {
        var user = await Db.Addresses
            .OfType<XAddress>()
            .FirstOrDefaultAsync(x => x.ChainId == state.Id && x.Hash == address);

        if (user == null)
            return;

        Cache.Chain.ReleaseAddressId();
        Cache.Addresses.Remove(user);
        Db.Addresses.Remove(user);

        state.MigrationOpsCount--;
        Cache.Chain.ReleaseOperationId();
        await Db.MigrationOps
            .Where(x => x.AddressId == user.Id)
            .ExecuteDeleteAsync();
    }
    #endregion

    #region legacy
    async Task<XEvmContract> BootstrapXEvmContract(string hash, byte[] code)
    {
        // the actual creator is unobservable without traces, therefore we use NullAddress
        var creator = (await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress)!;
        Db.TryAttach(creator);
        creator.LastLevel = Context.Block.Level;
        creator.LastTimestamp = Context.Block.Timestamp;

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
    #endregion
}
