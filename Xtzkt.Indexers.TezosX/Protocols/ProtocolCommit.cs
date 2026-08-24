using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public abstract class ProtocolCommit(ProtocolHandler protocol)
    {
        protected static readonly BigInteger M12 = new(1_000_000_000_000);

        protected readonly XtzktContext Db = protocol.Db;
        protected readonly CacheService Cache = protocol.Cache;
        protected readonly ProtocolHandler Proto = protocol;
        protected readonly BlockContext Context = protocol.Context;
        protected readonly ILogger Logger = protocol.Logger;

        #region addresses
        protected virtual async Task<XEvmAddress> GetOrCreateXEvmAddress(string hash)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(hash) is XEvmAddress address)
                return address;

            return await CreateXEvmUser(hash);
        }

        protected async Task RemoveXEvmAddress(XEvmAddress address)
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

        protected virtual async Task<XEvmUser> GetOrCreateXEvmUser(string hash)
        {
            var address = await Cache.Addresses.GetOrDefaultAsync(hash);
            if (address is XEvmUser user)
                return user;

            if (address is not null)
                throw new InvalidOperationException($"Cannot interpret {address.Type} as XEvmUser");

            return await CreateXEvmUser(hash);
        }

        protected async Task<XEvmUser> CreateXEvmUser(string hash)
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

        protected async Task RemoveXEvmUser(XEvmUser user)
        {
            if (user.AliasesCount != 0)
                await UnbindAliases(user);

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(user);
            Db.Addresses.Remove(user);
        }

        protected async Task<XEvmAlias> GetOrCreateXEvmAlias(string hash, XMichelsonAddress owner)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(hash) is XEvmAddress address)
            {
                if (address is XEvmAlias existingAlias)
                    return existingAlias;

                if (address is not XEvmUser ghost)
                    throw new InvalidOperationException($"Cannot upgrade {address.Type} to XEvmAlias");

                return await UpgradeToXEvmAlias(ghost, owner);
            }

            var aliasForwarder = await Cache.Addresses.GetExistingAsync(EvmRuntime.AliasForwarder);
            var alias = new XEvmAlias
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = Context.Block.ChainId,
                Hash = hash,
                FirstLevel = Context.Block.Level,
                FirstTimestamp = Context.Block.Timestamp,
                LastLevel = Context.Block.Level,
                LastTimestamp = Context.Block.Timestamp,
                Counter = -1, // counter keeps the last used nonce, for new address it's -1
                Eip7702DelegateId = aliasForwarder.Id,
                OwnerId = owner.Id,
            };

            owner.AliasesCount++;

            Context.Block.Events |= XBlockEvents.NewAddresses;

            Cache.Addresses.Add(alias);
            Db.Addresses.Add(alias);

            // TODO: check if aliases can have aliases
            await BindAliases(alias);

            return alias;
        }

        protected async Task<XEvmAlias> UpgradeToXEvmAlias(XEvmUser user, XMichelsonAddress owner)
        {
            var aliasForwarder = await Cache.Addresses.GetExistingAsync(EvmRuntime.AliasForwarder);
            var alias = new XEvmAlias
            {
                Id = user.Id,
                ChainId = user.ChainId,
                Hash = user.Hash,
                FirstLevel = user.FirstLevel,
                FirstTimestamp = user.FirstTimestamp,
                LastLevel = user.LastLevel,
                LastTimestamp = user.LastTimestamp,
                ActiveTicketsCount = user.ActiveTicketsCount,
                ActiveTokensCount = user.ActiveTokensCount,
                Balance = user.Balance,
                BlocksCount = user.BlocksCount,
                ContractsCount = user.ContractsCount,
                Counter = user.Counter,
                DepositOpsCount = user.DepositOpsCount,
                MigrationsCount = user.MigrationsCount,
                OriginationsCount = user.OriginationsCount,
                TicketBalancesCount = user.TicketBalancesCount,
                TicketTransfersCount = user.TicketTransfersCount,
                TokenBalancesCount = user.TokenBalancesCount,
                TokenTransfersCount = user.TokenTransfersCount,
                TransactionsCount = user.TransactionsCount,
                Eip7702DelegationCount = user.Eip7702DelegationCount,
                LogsCount = user.LogsCount,
                AliasesCount = user.AliasesCount,
                Eip7702DelegateId = aliasForwarder.Id,
                OwnerId = owner.Id,
            };
            Cache.Addresses.Add(alias);
            var isAdded = Db.Entry(user).State == EntityState.Added;
            Db.Entry(user).State = EntityState.Detached;
            Db.Entry(alias).State = isAdded ? EntityState.Added : EntityState.Modified;

            owner.AliasesCount++;

            return alias;
        }

        protected void DowngradeToXEvmUser(XEvmAlias alias, XMichelsonAddress owner)
        {
            var user = new XEvmUser
            {
                Id = alias.Id,
                ChainId = alias.ChainId,
                Hash = alias.Hash,
                FirstLevel = alias.FirstLevel,
                FirstTimestamp = alias.FirstTimestamp,
                LastLevel = alias.LastLevel,
                LastTimestamp = alias.LastTimestamp,
                ActiveTicketsCount = alias.ActiveTicketsCount,
                ActiveTokensCount = alias.ActiveTokensCount,
                Balance = alias.Balance,
                BlocksCount = alias.BlocksCount,
                ContractsCount = alias.ContractsCount,
                Counter = alias.Counter,
                DepositOpsCount = alias.DepositOpsCount,
                MigrationsCount = alias.MigrationsCount,
                OriginationsCount = alias.OriginationsCount,
                TicketBalancesCount = alias.TicketBalancesCount,
                TicketTransfersCount = alias.TicketTransfersCount,
                TokenBalancesCount = alias.TokenBalancesCount,
                TokenTransfersCount = alias.TokenTransfersCount,
                TransactionsCount = alias.TransactionsCount,
                Eip7702DelegationCount = alias.Eip7702DelegationCount,
                LogsCount = alias.LogsCount,
                AliasesCount = alias.AliasesCount,
                Eip7702DelegateId = null,
            };
            Cache.Addresses.Add(user);
            Db.Entry(alias).State = EntityState.Detached;
            Db.Entry(user).State = EntityState.Modified;

            owner.AliasesCount--;
        }

        protected async Task RemoveXEvmAlias(XEvmAlias alias, XMichelsonAddress owner)
        {
            // TODO: check if aliases can have aliases
            if (alias.AliasesCount != 0)
                await UnbindAliases(alias);

            owner.AliasesCount--;

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(alias);
            Db.Addresses.Remove(alias);
        }

        protected async Task BindAliases(XEvmAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(MichelsonRuntime.GetAlias(address.Hash)) is XMichelsonAddress alias)
            {
                if (alias is not XMichelsonGhost ghost)
                    throw new InvalidOperationException($"Cannot upgrade {alias.Type} to XMichelsonAlias");

                UpgradeToXMichelsonAlias(ghost, address);
            }
            // UPDATE: add other runtimes here, when implemented
        }

        protected async Task UnbindAliases(XEvmAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(MichelsonRuntime.GetAlias(address.Hash)) is XMichelsonAlias alias)
                DowngradeToXMichelsonGhost(alias, address);

            // UPDATE: add other runtimes here, when implemented

            if (address.AliasesCount != 0)
                throw new InvalidOperationException("Failed to unbind aliases");
        }

        protected async Task<XEvmContract> CreateXEvmContract(string hash, XEvmAddress creator)
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

        protected XEvmContract UpgradeToXEvmContract(XEvmUser ghost, XEvmAddress creator)
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

        protected async Task RemoveXEvmContract(XEvmContract contract, XEvmAddress creator)
        {
            if (!contract.IsEmpty())
            {
                DowngradeToXEvmUser(contract, creator);
                return;
            }

            if (contract.AliasesCount != 0)
                await UnbindAliases(contract);

            creator.ContractsCount--;

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(contract);
            Db.Addresses.Remove(contract);
        }


        protected async Task<XMichelsonAddress> GetOrCreateXMichelsonAddress(string hash)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(hash) is XMichelsonAddress address)
                return address;

            return hash[0] == 't'
                ? await CreateXMichelsonUser(hash, Context.Block)
                : await CreateXMichelsonGhost(hash, Context.Block);
        }

        protected async Task RemoveXMichelsonAddress(XMichelsonAddress address)
        {
            if (address is XMichelsonUser user)
            {
                await RemoveXMichelsonUser(user);
            }
            else if (address is XMichelsonGhost ghost)
            {
                await RemoveXMichelsonGhost(ghost);
            }
            else if (address is XMichelsonAlias alias)
            {
                var owner = (await Cache.Addresses.GetAsync(alias.OwnerId) as XEvmAddress)!;
                await RemoveXMichelsonAlias(alias, owner);
            }
            else
            {
                throw new InvalidOperationException($"Cannot remove {address.Type}");
            }
        }

        protected async Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash)
        {
            if (Cache.Addresses.TryGetCached(hash, out var address))
                return (address as XMichelsonAddress)!;

            return hash[0] == 't'
                ? await CreateXMichelsonUser(hash, Context.Block)
                : await CreateXMichelsonGhost(hash, Context.Block);
        }

        protected async Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash, XBlock block)
        {
            if (Cache.Addresses.TryGetCached(hash, out var address))
                return (address as XMichelsonAddress)!;

            return hash[0] == 't'
                ? await CreateXMichelsonUser(hash, block)
                : await CreateXMichelsonGhost(hash, block);
        }

        public async Task<XMichelsonUser> GetOrCreateXMichelsonUser(string hash)
        {
            var address = await Cache.Addresses.GetOrDefaultAsync(hash);
            if (address is XMichelsonUser user)
                return user;

            if (address is not null)
                throw new InvalidOperationException($"Cannot interpret {address.Type} as XMichelsonUser");

            return await CreateXMichelsonUser(hash, Context.Block);
        }

        protected async Task<XMichelsonUser> CreateXMichelsonUser(string hash, XBlock block)
        {
            var user = new XMichelsonUser
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = block.ChainId,
                Hash = hash,
                FirstLevel = block.Level,
                FirstTimestamp = block.Timestamp,
                LastLevel = block.Level,
                LastTimestamp = block.Timestamp,
            };

            block.Events |= XBlockEvents.NewAddresses;

            Cache.Addresses.Add(user);
            Db.Addresses.Add(user);

            await BindAliases(user);

            return user;
        }

        protected async Task RemoveXMichelsonUser(XMichelsonUser user)
        {
            if (user.AliasesCount != 0)
                await UnbindAliases(user);

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(user);
            Db.Addresses.Remove(user);
        }

        protected async Task<XMichelsonGhost> CreateXMichelsonGhost(string hash, XBlock block)
        {
            var ghost = new XMichelsonGhost
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = block.ChainId,
                Hash = hash,
                FirstLevel = block.Level,
                FirstTimestamp = block.Timestamp,
                LastLevel = block.Level,
                LastTimestamp = block.Timestamp,
            };

            block.Events |= XBlockEvents.NewAddresses;

            Cache.Addresses.Add(ghost);
            Db.Addresses.Add(ghost);

            await BindAliases(ghost);

            return ghost;
        }

        protected async Task RemoveXMichelsonGhost(XMichelsonGhost ghost)
        {
            if (ghost.AliasesCount != 0)
                await UnbindAliases(ghost);

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(ghost);
            Db.Addresses.Remove(ghost);
        }

        protected async Task<XMichelsonAlias> GetOrCreateXMichelsonAlias(string hash, XEvmAddress owner)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(hash) is XMichelsonAddress address)
            {
                if (address is XMichelsonAlias existingAlias)
                    return existingAlias;

                if (address is not XMichelsonGhost ghost)
                    throw new InvalidOperationException($"Cannot upgrade {address.Type} to XMichelsonAlias");

                return UpgradeToXMichelsonAlias(ghost, owner);
            }

            var alias = new XMichelsonAlias
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = Context.Block.ChainId,
                Hash = hash,
                FirstLevel = Context.Block.Level,
                FirstTimestamp = Context.Block.Timestamp,
                LastLevel = Context.Block.Level,
                LastTimestamp = Context.Block.Timestamp,
                OwnerId = owner.Id,
            };

            owner.AliasesCount++;

            Context.Block.Events |= XBlockEvents.NewAddresses;

            Cache.Addresses.Add(alias);
            Db.Addresses.Add(alias);

            // TODO: check if aliases can have aliases
            await BindAliases(alias);

            return alias;
        }

        protected XMichelsonAlias UpgradeToXMichelsonAlias(XMichelsonGhost ghost, XEvmAddress owner)
        {
            var alias = new XMichelsonAlias
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
                ContractsCount = ghost.ContractsCount,
                DepositOpsCount = ghost.DepositOpsCount,
                MigrationsCount = ghost.MigrationsCount,
                OriginationsCount = ghost.OriginationsCount,
                TicketBalancesCount = ghost.TicketBalancesCount,
                TicketTransfersCount = ghost.TicketTransfersCount,
                TokenBalancesCount = ghost.TokenBalancesCount,
                TokenTransfersCount = ghost.TokenTransfersCount,
                TransactionsCount = ghost.TransactionsCount,
                IncreasePaidStorageCount = ghost.IncreasePaidStorageCount,
                Index = ghost.Index,
                TransferTicketCount = ghost.TransferTicketCount,
                AliasesCount = ghost.AliasesCount,
                OwnerId = owner.Id,
            };
            Cache.Addresses.Add(alias);
            var isAdded = Db.Entry(ghost).State == EntityState.Added;
            Db.Entry(ghost).State = EntityState.Detached;
            Db.Entry(alias).State = isAdded ? EntityState.Added : EntityState.Modified;

            owner.AliasesCount++;

            return alias;
        }

        protected void DowngradeToXMichelsonGhost(XMichelsonAlias alias, XEvmAddress owner)
        {
            var ghost = new XMichelsonGhost
            {
                Id = alias.Id,
                ChainId = alias.ChainId,
                Hash = alias.Hash,
                FirstLevel = alias.FirstLevel,
                FirstTimestamp = alias.FirstTimestamp,
                LastLevel = alias.LastLevel,
                LastTimestamp = alias.LastTimestamp,
                ActiveTicketsCount = alias.ActiveTicketsCount,
                ActiveTokensCount = alias.ActiveTokensCount,
                Balance = alias.Balance,
                ContractsCount = alias.ContractsCount,
                DepositOpsCount = alias.DepositOpsCount,
                MigrationsCount = alias.MigrationsCount,
                OriginationsCount = alias.OriginationsCount,
                TicketBalancesCount = alias.TicketBalancesCount,
                TicketTransfersCount = alias.TicketTransfersCount,
                TokenBalancesCount = alias.TokenBalancesCount,
                TokenTransfersCount = alias.TokenTransfersCount,
                TransactionsCount = alias.TransactionsCount,
                IncreasePaidStorageCount = alias.IncreasePaidStorageCount,
                Index = alias.Index,
                TransferTicketCount = alias.TransferTicketCount,
                AliasesCount = alias.AliasesCount,
            };
            Cache.Addresses.Add(ghost);
            Db.Entry(alias).State = EntityState.Detached;
            Db.Entry(ghost).State = EntityState.Modified;

            owner.AliasesCount--;
        }

        protected async Task RemoveXMichelsonAlias(XMichelsonAlias alias, XEvmAddress owner)
        {
            // TODO: check if aliases can have aliases
            if (alias.AliasesCount != 0)
                await UnbindAliases(alias);

            owner.AliasesCount--;

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(alias);
            Db.Addresses.Remove(alias);
        }

        protected async Task BindAliases(XMichelsonAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(EvmRuntime.GetAlias(address.Hash)) is XEvmAddress alias)
            {
                if (alias is not XEvmUser ghost)
                    throw new InvalidOperationException($"Cannot upgrade {alias.Type} to XMichelsonAlias");

                await UpgradeToXEvmAlias(ghost, address);
            }
            // UPDATE: add other runtimes here, when implemented
        }

        protected async Task UnbindAliases(XMichelsonAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(EvmRuntime.GetAlias(address.Hash)) is XEvmAlias alias)
                DowngradeToXEvmUser(alias, address);

            // NOTE: add other runtimes, when implemented

            if (address.AliasesCount != 0)
                throw new InvalidOperationException("Failed to unbind aliases");
        }

        protected async Task<XMichelsonContract> CreateXMichelsonContract(string hash, XMichelsonAddress creator)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(hash) is XMichelsonAddress address)
            {
                if (address is XMichelsonContract)
                    throw new InvalidOperationException($"Contract {hash} already exists");

                if (address is not XMichelsonGhost ghost)
                    throw new InvalidOperationException($"Cannot upgrade {address.Type} to XMichelsonContract");

                return UpgradeToXMichelsonContract(ghost, creator);
            }

            var contract = new XMichelsonContract
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = Context.Block.ChainId,
                Hash = hash,
                FirstLevel = Context.Block.Level,
                FirstTimestamp = Context.Block.Timestamp,
                LastLevel = Context.Block.Level,
                LastTimestamp = Context.Block.Timestamp,
                CreatorId = creator.Id,
                Kind = XContractKind.SmartContract,
            };

            creator.ContractsCount++;

            Context.Block.Events |= XBlockEvents.NewAddresses;

            Cache.Addresses.Add(contract);
            Db.Addresses.Add(contract);

            await BindAliases(contract);

            return contract;
        }

        protected XMichelsonContract UpgradeToXMichelsonContract(XMichelsonGhost ghost, XMichelsonAddress creator)
        {
            var contract = new XMichelsonContract
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
                ContractsCount = ghost.ContractsCount,
                DepositOpsCount = ghost.DepositOpsCount,
                MigrationsCount = ghost.MigrationsCount,
                OriginationsCount = ghost.OriginationsCount,
                TicketBalancesCount = ghost.TicketBalancesCount,
                TicketTransfersCount = ghost.TicketTransfersCount,
                TokenBalancesCount = ghost.TokenBalancesCount,
                TokenTransfersCount = ghost.TokenTransfersCount,
                TransactionsCount = ghost.TransactionsCount,
                AliasesCount = ghost.AliasesCount,
                Index = ghost.Index,
                IncreasePaidStorageCount = ghost.IncreasePaidStorageCount,
                TransferTicketCount = ghost.TransferTicketCount,
                CreatorId = creator.Id,
                Kind = XContractKind.SmartContract,
                Tags = XMichelsonContractTags.None,
                CodeHash = 0,
                TypeHash = 0,
                LogsCount = 0,
                TicketsCount = 0,
                TokensCount = 0,
            };
            Cache.Addresses.Add(contract);
            var isAdded = Db.Entry(ghost).State == EntityState.Added;
            Db.Entry(ghost).State = EntityState.Detached;
            Db.Entry(contract).State = isAdded ? EntityState.Added : EntityState.Modified;

            creator.ContractsCount++;

            return contract;
        }

        protected void DowngradeToXMichelsonGhost(XMichelsonContract contract, XMichelsonAddress creator)
        {
            var ghost = new XMichelsonGhost
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
                ContractsCount = contract.ContractsCount,
                DepositOpsCount = contract.DepositOpsCount,
                MigrationsCount = contract.MigrationsCount,
                OriginationsCount = contract.OriginationsCount,
                TicketBalancesCount = contract.TicketBalancesCount,
                TicketTransfersCount = contract.TicketTransfersCount,
                TokenBalancesCount = contract.TokenBalancesCount,
                TokenTransfersCount = contract.TokenTransfersCount,
                TransactionsCount = contract.TransactionsCount,
                AliasesCount = contract.AliasesCount,
                Index = contract.Index,
                IncreasePaidStorageCount = contract.IncreasePaidStorageCount,
                TransferTicketCount = contract.TransferTicketCount,
            };
            Cache.Addresses.Add(ghost);
            Db.Entry(contract).State = EntityState.Detached;
            Db.Entry(ghost).State = EntityState.Modified;

            creator.ContractsCount--;
        }

        protected async Task RemoveXMichelsonContract(XMichelsonContract contract, XMichelsonAddress creator)
        {
            if (!contract.IsEmpty())
            {
                DowngradeToXMichelsonGhost(contract, creator);
                return;
            }

            if (contract.AliasesCount != 0)
                await UnbindAliases(contract);

            creator.ContractsCount--;

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(contract);
            Db.Addresses.Remove(contract);
        }
        #endregion

        #region fees
        protected void PayFee(XMichelsonAddress address, long daFee)
        {
            Spend(address, daFee);
            var daFee18 = new BigInteger(daFee) * M12;
            Context.Block.DaFees += daFee18;
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                Receive(sequencerPool, daFee18);
            else
                Context.Statistics.TotalBurned += daFee18;
        }

        protected void RevertPayFee(XMichelsonAddress address, long daFee)
        {
            RevertSpend(address, daFee);
            var daFee18 = new BigInteger(daFee) * M12;
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                RevertReceive(sequencerPool, daFee18);
        }

        protected void PayFee(XEvmAddress address, BigInteger daFee)
        {
            Spend(address, daFee);
            Context.Block.DaFees += daFee;
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                Receive(sequencerPool, daFee);
            else
                Context.Statistics.TotalBurned += daFee;
        }

        protected void RevertPayFee(XEvmAddress address, BigInteger daFee)
        {
            RevertSpend(address, daFee);
            if (Context.SequencerPool is XEvmAddress sequencerPool)
                RevertReceive(sequencerPool, daFee);
        }

        protected void BurnFee(XEvmAddress address, BigInteger fee)
        {
            Spend(address, fee);
            Context.Block.BurnedFees += fee;
            Context.Statistics.TotalBurned += fee;
        }

        protected void RevertBurnFee(XEvmAddress address, BigInteger fee)
        {
            RevertSpend(address, fee);
        }

        protected void BurnFee(XMichelsonAddress address, long fee)
        {
            Spend(address, fee);
            var fee18 = new BigInteger(fee) * M12;
            Context.Block.BurnedFees += fee18;
            Context.Statistics.TotalBurned += fee18;
        }

        protected void RevertBurnFee(XMichelsonAddress address, long fee)
        {
            RevertSpend(address, fee);
        }
        #endregion

        #region money flow
        protected void Spend(XEvmAddress address, BigInteger amount)
        {
            address.Balance -= amount;
        }

        protected void RevertSpend(XEvmAddress address, BigInteger amount)
        {
            address.Balance += amount;
        }

        protected void Spend(XMichelsonAddress address, long amount)
        {
            address.Balance -= amount;
        }

        protected void RevertSpend(XMichelsonAddress address, long amount)
        {
            address.Balance += amount;
        }

        protected void Receive(XEvmAddress address, BigInteger amount)
        {
            address.Balance += amount;
        }

        protected void RevertReceive(XEvmAddress address, BigInteger amount)
        {
            address.Balance -= amount;
        }

        protected void Receive(XMichelsonAddress address, long amount)
        {
            address.Balance += amount;
        }

        protected void RevertReceive(XMichelsonAddress address, long amount)
        {
            address.Balance -= amount;
        }
        #endregion

        #region helpers
        protected (long? StorageFee, long? AllocationFee) GetStorageFees(JsonElement result, bool allocated, int? paidStorageSize = null)
        {
            var totalBurned = result
                .OptionalArray("balance_updates")?
                .EnumerateArray()
                .Where(x => x.RequiredString("kind") == "burned" && x.RequiredString("category") == "storage fees")
                .Sum(x => x.RequiredInt64("change"))
                ?? 0;

            if (totalBurned == 0)
                return (null, null);

            var allocationFee = allocated ? Context.Protocol.OriginationSize * Context.Protocol.ByteCost : 0;
            if (allocationFee > totalBurned)
                throw new ValidationException("Unexpected allocation burn");

            var storageFee = totalBurned - allocationFee;
            if (paidStorageSize is int size && storageFee != size * Context.Protocol.ByteCost)
                throw new ValidationException("Unexpected storage burn");

            return (storageFee != 0 ? storageFee : null, allocationFee != 0 ? allocationFee : null);
        }

        // eip6780: selfdestruct deletes the account, burning its balance along with it, but only if
        // the contract was created in the same transaction and is its own beneficiary
        protected bool IsSelfDestructWithBurn(XEvmTransactionOperation op)
        {
            return op.OpCode is EvmOpCode.SelfDestruct or EvmOpCode.Suicide
                && op.SenderId == op.TargetId
                && op.Amount != 0
                && Context.OriginationOps.Any(x => x.Hash == op.Hash && x.ContractId == op.SenderId);
        }

        protected static OperationStatus GetEvmTraceStatus(OperationStatus rootStatus, OperationStatus traceStatus)
        {
            return rootStatus != OperationStatus.Applied && traceStatus == OperationStatus.Applied
                ? OperationStatus.Backtracked
                : traceStatus;
        }

        protected async Task<XEvmAddress?> GetEip7702Delegate(XEvmAddress address)
        {
            if (address is XEvmUser user && user.Eip7702DelegateId is int userDelegateId)
                return await Cache.Addresses.GetAsync(userDelegateId) as XEvmAddress;

            if (address is XEvmAlias alias && alias.Eip7702DelegateId is int aliasDelegateId)
                return await Cache.Addresses.GetAsync(aliasDelegateId) as XEvmAddress;

            return null;
        }

        protected static int SubcallsGasUsed(JsonElement trace)
        {
            return trace.OptionalArray("calls")?.EnumerateArray().Sum(x => x.RequiredHexInt32("gasUsed")) ?? 0;
        }
        #endregion
    }
}
