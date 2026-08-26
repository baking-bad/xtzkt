using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers
{
    public partial class ProtoHelpers
    {
        public virtual async Task<XEvmAddress> GetOrCreateXEvmAddress(string hash)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(hash) is XEvmAddress address)
                return address;

            return await CreateXEvmUser(hash);
        }

        public virtual async Task RemoveXEvmAddress(XEvmAddress address)
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

        public virtual async Task<XEvmUser> GetOrCreateXEvmUser(string hash)
        {
            var address = await Cache.Addresses.GetOrDefaultAsync(hash);
            if (address is XEvmUser user)
                return user;

            if (address is not null)
                throw new InvalidOperationException($"Cannot interpret {address.Type} as XEvmUser");

            return await CreateXEvmUser(hash);
        }

        public virtual async Task<XEvmUser> CreateXEvmUser(string hash)
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

        public virtual async Task RemoveXEvmUser(XEvmUser user)
        {
            if (user.AliasesCount != 0)
                await UnbindAliases(user);

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(user);
            Db.Addresses.Remove(user);
        }

        public virtual async Task<XEvmAlias> GetOrCreateXEvmAlias(string hash, XMichelsonAddress owner)
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

        protected virtual async Task<XEvmAlias> UpgradeToXEvmAlias(XEvmUser user, XMichelsonAddress owner)
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

        protected virtual void DowngradeToXEvmUser(XEvmAlias alias, XMichelsonAddress owner)
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

        public virtual async Task RemoveXEvmAlias(XEvmAlias alias, XMichelsonAddress owner)
        {
            // TODO: check if aliases can have aliases
            if (alias.AliasesCount != 0)
                await UnbindAliases(alias);

            owner.AliasesCount--;

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(alias);
            Db.Addresses.Remove(alias);
        }

        protected virtual async Task BindAliases(XEvmAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(MichelsonRuntime.GetAlias(address.Hash)) is XMichelsonAddress alias)
            {
                if (alias is not XMichelsonGhost ghost)
                    throw new InvalidOperationException($"Cannot upgrade {alias.Type} to XMichelsonAlias");

                UpgradeToXMichelsonAlias(ghost, address);
            }
            // UPDATE: add other runtimes here, when implemented
        }

        protected virtual async Task UnbindAliases(XEvmAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(MichelsonRuntime.GetAlias(address.Hash)) is XMichelsonAlias alias)
                DowngradeToXMichelsonGhost(alias, address);

            // UPDATE: add other runtimes here, when implemented

            if (address.AliasesCount != 0)
                throw new InvalidOperationException("Failed to unbind aliases");
        }

        public virtual Task<XEvmContract> GetOrCreateXEvmContract(string hash)
        {
            // only makes sense in the genesis era, where contract creators are unobservable without call traces
            throw new NotImplementedException();
        }

        public virtual async Task<XEvmContract> CreateXEvmContract(string hash, XEvmAddress creator)
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

        public virtual XEvmContract UpgradeToXEvmContract(XEvmUser ghost, XEvmAddress creator)
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

        protected virtual void DowngradeToXEvmUser(XEvmContract contract, XEvmAddress creator)
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

        public virtual async Task RemoveXEvmContract(XEvmContract contract, XEvmAddress creator)
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


        public virtual async Task<XMichelsonAddress> GetOrCreateXMichelsonAddress(string hash)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(hash) is XMichelsonAddress address)
                return address;

            return hash[0] == 't'
                ? await CreateXMichelsonUser(hash, Context.Block)
                : await CreateXMichelsonGhost(hash, Context.Block);
        }

        public virtual async Task RemoveXMichelsonAddress(XMichelsonAddress address)
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

        public virtual async Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash)
        {
            if (Cache.Addresses.TryGetCached(hash, out var address))
                return (address as XMichelsonAddress)!;

            return hash[0] == 't'
                ? await CreateXMichelsonUser(hash, Context.Block)
                : await CreateXMichelsonGhost(hash, Context.Block);
        }

        public virtual async Task<XMichelsonAddress> GetCachedOrCreateXMichelsonAddress(string hash, XBlock block)
        {
            if (Cache.Addresses.TryGetCached(hash, out var address))
                return (address as XMichelsonAddress)!;

            return hash[0] == 't'
                ? await CreateXMichelsonUser(hash, block)
                : await CreateXMichelsonGhost(hash, block);
        }

        public virtual async Task<XMichelsonUser> GetOrCreateXMichelsonUser(string hash)
        {
            var address = await Cache.Addresses.GetOrDefaultAsync(hash);
            if (address is XMichelsonUser user)
                return user;

            if (address is not null)
                throw new InvalidOperationException($"Cannot interpret {address.Type} as XMichelsonUser");

            return await CreateXMichelsonUser(hash, Context.Block);
        }

        protected virtual async Task<XMichelsonUser> CreateXMichelsonUser(string hash, XBlock block)
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

        public virtual async Task RemoveXMichelsonUser(XMichelsonUser user)
        {
            if (user.AliasesCount != 0)
                await UnbindAliases(user);

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(user);
            Db.Addresses.Remove(user);
        }

        protected virtual async Task<XMichelsonGhost> CreateXMichelsonGhost(string hash, XBlock block)
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

        protected virtual async Task RemoveXMichelsonGhost(XMichelsonGhost ghost)
        {
            if (ghost.AliasesCount != 0)
                await UnbindAliases(ghost);

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(ghost);
            Db.Addresses.Remove(ghost);
        }

        public virtual async Task<XMichelsonAlias> GetOrCreateXMichelsonAlias(string hash, XEvmAddress owner)
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

        protected virtual XMichelsonAlias UpgradeToXMichelsonAlias(XMichelsonGhost ghost, XEvmAddress owner)
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

        protected virtual void DowngradeToXMichelsonGhost(XMichelsonAlias alias, XEvmAddress owner)
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

        public virtual async Task RemoveXMichelsonAlias(XMichelsonAlias alias, XEvmAddress owner)
        {
            // TODO: check if aliases can have aliases
            if (alias.AliasesCount != 0)
                await UnbindAliases(alias);

            owner.AliasesCount--;

            Cache.Chain.ReleaseAddressId();
            Cache.Addresses.Remove(alias);
            Db.Addresses.Remove(alias);
        }

        protected virtual async Task BindAliases(XMichelsonAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(EvmRuntime.GetAlias(address.Hash)) is XEvmAddress alias)
            {
                if (alias is not XEvmUser ghost)
                    throw new InvalidOperationException($"Cannot upgrade {alias.Type} to XMichelsonAlias");

                await UpgradeToXEvmAlias(ghost, address);
            }
            // UPDATE: add other runtimes here, when implemented
        }

        protected virtual async Task UnbindAliases(XMichelsonAddress address)
        {
            if (await Cache.Addresses.GetOrDefaultAsync(EvmRuntime.GetAlias(address.Hash)) is XEvmAlias alias)
                DowngradeToXEvmUser(alias, address);

            // NOTE: add other runtimes, when implemented

            if (address.AliasesCount != 0)
                throw new InvalidOperationException("Failed to unbind aliases");
        }

        public virtual async Task<XMichelsonContract> CreateXMichelsonContract(string hash, XMichelsonAddress creator)
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

        protected virtual XMichelsonContract UpgradeToXMichelsonContract(XMichelsonGhost ghost, XMichelsonAddress creator)
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

        protected virtual void DowngradeToXMichelsonGhost(XMichelsonContract contract, XMichelsonAddress creator)
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

        public virtual async Task RemoveXMichelsonContract(XMichelsonContract contract, XMichelsonAddress creator)
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
    }
}
