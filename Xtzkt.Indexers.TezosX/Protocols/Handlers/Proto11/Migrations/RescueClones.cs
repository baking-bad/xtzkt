using Microsoft.EntityFrameworkCore;
using Netezos;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11;

class RescueClones(ProtocolHandler proto) : ProtocolCommit(proto)
{
    // EIP-1167 proxy head (10), implementation (20), proxy tail (15), key hash of the owner (20)
    const int CloneCodeSize = 65;

    public async Task Bind(XChain state)
    {
        var candidates = await Db.Scripts
            .OfType<EvmScript>()
            .Where(x => x.ChainId == state.Id && x.Current && x.Code.Length == CloneCodeSize)
            .Select(x => new { x.ContractId, x.Code })
            .ToListAsync();

        var count = 0;
        foreach (var candidate in candidates.OrderBy(x => x.ContractId))
        {
            if (await Cache.Addresses.GetAsync(candidate.ContractId) is not XEvmContract contract)
                continue;

            var pkh = candidate.Code[^20..];
            foreach (var prefix in (byte[][])[Prefixes.tz1, Prefixes.tz2, Prefixes.tz3])
            {
                var owner = Base58.Encode(pkh, prefix);
                if (Runtimes.GetRecoverableEvmAlias(owner) == contract.Hash)
                {
                    await Bind(state, contract, owner);
                    count++;
                    break;
                }
            }
        }

        // TODO: if there were 0 clones, drop this whole logic
        Logger.LogInformation("{count} rescue clones were converted into aliases", count);
    }

    async Task Bind(XChain state, XEvmContract contract, string ownerHash)
    {
        var block = Context.Block;

        if (await Cache.Addresses.GetOrDefaultAsync(ownerHash) is not XMichelsonAddress owner)
        {
            owner = new XMichelsonUser
            {
                Id = Cache.Chain.NextAddressId(),
                ChainId = block.ChainId,
                Hash = ownerHash,
                FirstLevel = block.Level,
                FirstTimestamp = block.Timestamp,
                LastLevel = block.Level,
                LastTimestamp = block.Timestamp,
            };

            block.Events |= XBlockEvents.NewAddresses;

            Cache.Addresses.Add(owner);
            Db.Addresses.Add(owner);
        }

        var aliasForwarder = await Cache.Addresses.GetExistingAsync(EvmRuntime.AliasForwarder);
        var alias = new XEvmAlias
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
            Counter = contract.Counter,
            DepositOpsCount = contract.DepositOpsCount,
            MigrationsCount = contract.MigrationsCount,
            OriginationsCount = contract.OriginationsCount,
            TicketBalancesCount = contract.TicketBalancesCount,
            TicketTransfersCount = contract.TicketTransfersCount,
            TokenBalancesCount = contract.TokenBalancesCount,
            TokenTransfersCount = contract.TokenTransfersCount,
            TransactionsCount = contract.TransactionsCount,
            Eip7702DelegationCount = contract.Eip7702DelegationCount,
            LogsCount = contract.LogsCount,
            AliasesCount = contract.AliasesCount,
            ActiveBridgeTicketsCount = contract.ActiveBridgeTicketsCount,
            BridgeTicketBalancesCount = contract.BridgeTicketBalancesCount,
            BridgeTicketTransfersCount = contract.BridgeTicketTransfersCount,
            Eip7702DelegateId = aliasForwarder.Id,
            OwnerId = owner.Id,
        };
        // the contract columns stay in the row untouched, which is what the revert restores the contract from
        Cache.Addresses.Add(alias);
        Cache.Abi.Remove(contract);
        Db.Entry(contract).State = EntityState.Detached;
        Db.Entry(alias).State = EntityState.Modified;

        Db.TryAttach(owner);
        owner.AliasesCount++;

        AddMigration(state, alias, new EvmMigrationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            AddressId = alias.Id,
            Kind = MigrationKind.BindAlias,
        });

        AddMigration(state, owner, new MichelsonMigrationOperation
        {
            Id = Cache.Chain.NextOperationId(),
            ChainId = block.ChainId,
            Level = block.Level,
            Timestamp = block.Timestamp,
            AddressId = owner.Id,
            Kind = MigrationKind.BindAlias,
        });
    }

    void AddMigration(XChain state, XAddress address, MigrationOperation migration)
    {
        Db.TryAttach(address);
        address.MigrationsCount++;
        address.LastLevel = migration.Level;
        address.LastTimestamp = migration.Timestamp;

        state.MigrationOpsCount++;

        Context.Block.Operations |= XOperations.Migration;

        Context.MigrationOps.Add(migration);
        Db.MigrationOps.Add(migration);
    }

    public async Task Unbind(XChain state, bool keepOwners = true)
    {
        var (lo, hi) = IdLayout.Id64Range(Context.Block.ChainId, Context.Block.Id);
        var migrations = await Db.MigrationOps
            .Where(x => x.Id >= lo && x.Id <= hi && x.Kind == MigrationKind.BindAlias)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        for (var i = 0; i < migrations.Count; i += 2)
        {
            var ownerMigration = (MichelsonMigrationOperation)migrations[i];
            var aliasMigration = (EvmMigrationOperation)migrations[i + 1];

            var alias = (XEvmAlias)await Cache.Addresses.GetAsync(aliasMigration.AddressId);

            var columns = await Db.Database
                .SqlQuery<ContractColumns>($"""
                    SELECT "CreatorId", "Kind", "Tags", "TokensCount", "CodeHash"
                    FROM "Addresses"
                    WHERE "Id" = {alias.Id}
                    """)
                .SingleAsync();

            var contract = new XEvmContract
            {
                Id = alias.Id,
                ChainId = alias.ChainId,
                Hash = alias.Hash,
                FirstLevel = alias.FirstLevel,
                FirstTimestamp = alias.FirstTimestamp,
                LastLevel = Context.Block.Level,
                LastTimestamp = Context.Block.Timestamp,
                ActiveTicketsCount = alias.ActiveTicketsCount,
                ActiveTokensCount = alias.ActiveTokensCount,
                Balance = alias.Balance,
                BlocksCount = alias.BlocksCount,
                ContractsCount = alias.ContractsCount,
                Counter = alias.Counter,
                DepositOpsCount = alias.DepositOpsCount,
                MigrationsCount = alias.MigrationsCount - 1,
                OriginationsCount = alias.OriginationsCount,
                TicketBalancesCount = alias.TicketBalancesCount,
                TicketTransfersCount = alias.TicketTransfersCount,
                TokenBalancesCount = alias.TokenBalancesCount,
                TokenTransfersCount = alias.TokenTransfersCount,
                TransactionsCount = alias.TransactionsCount,
                Eip7702DelegationCount = alias.Eip7702DelegationCount,
                LogsCount = alias.LogsCount,
                AliasesCount = alias.AliasesCount,
                ActiveBridgeTicketsCount = alias.ActiveBridgeTicketsCount,
                BridgeTicketBalancesCount = alias.BridgeTicketBalancesCount,
                BridgeTicketTransfersCount = alias.BridgeTicketTransfersCount,
                CreatorId = columns.CreatorId,
                Kind = (XContractKind)columns.Kind,
                Tags = (XEvmContractTags)columns.Tags,
                TokensCount = columns.TokensCount,
                CodeHash = columns.CodeHash,
            };
            Cache.Addresses.Add(contract);
            Db.Entry(alias).State = EntityState.Detached;
            Db.Entry(contract).State = EntityState.Modified;

            // the alias columns would be left behind the same way, and a stale delegate or owner id must not stay in the row
            await Db.Database.ExecuteSqlAsync($"""
                UPDATE "Addresses"
                SET "OwnerId" = NULL, "Eip7702DelegateId" = NULL
                WHERE "Id" = {alias.Id}
                """);

            if (keepOwners)
            {
                var owner = (XMichelsonAddress)await Cache.Addresses.GetAsync(ownerMigration.AddressId);
                Db.TryAttach(owner);
                owner.AliasesCount--;
                owner.MigrationsCount--;
                owner.LastLevel = Context.Block.Level;
                owner.LastTimestamp = Context.Block.Timestamp;

                if (owner.FirstLevel == Context.Block.Level && owner.IsEmpty())
                {
                    Cache.Chain.ReleaseAddressId();
                    Cache.Addresses.Remove(owner);
                    Db.Addresses.Remove(owner);
                }
            }

            Db.MigrationOps.Remove(ownerMigration);
            Db.MigrationOps.Remove(aliasMigration);

            state.MigrationOpsCount -= 2;
            Cache.Chain.ReleaseOperationId(2);
        }
    }

    sealed class ContractColumns
    {
        public int CreatorId { get; set; }
        public int Kind { get; set; }
        public int Tags { get; set; }
        public int TokensCount { get; set; }
        public int CodeHash { get; set; }
    }
}
