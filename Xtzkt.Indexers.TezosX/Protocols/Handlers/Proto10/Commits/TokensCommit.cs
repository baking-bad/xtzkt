using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class TokensCommit(ProtocolHandler protocol) : Proto02.TokensCommit(protocol)
{
    public virtual async Task Apply(List<(BigMap BigMap, BigMapKey? Key, BigMapUpdate Update, IBigmapOperation Op)> updates)
    {
        var block = Context.Block;
        updates = [.. updates.OrderBy(x => x.Op.Id).ThenBy(x => x.BigMap.Ptr)];
        var ops = new Dictionary<IBigmapOperation, (
            bool Reset,
            XMichelsonContract Contract,
            Dictionary<BigInteger, (
                List<(string From, byte[]? FromEp, string To, byte[]? ToEp, BigInteger Amount)> Transfers,
                List<(string Address, byte[]? Ep, BigInteger Balance)> Balances
            )> Tokens
        )>();
        var opBlocks = new Dictionary<int, XBlock> { { block.Level, block } };

        #region discover ledgers
        Dictionary<int, BigMap>? pendingBigMaps = null;
        foreach (var (bigmap, _, update, op) in updates)
        {
            if (update.Action == BigMapAction.Allocate)
            {
                if ((bigmap.Tags & BigMapTag.LedgerTypes) != 0)
                {
                    var contract = (await Cache.Addresses.GetAsync(
                        op is TransactionOperation tx
                            ? tx.TargetId
                            : (op as OriginationOperation)!.ContractId!.Value
                    ) as XMichelsonContract)!;

                    if (contract.Tags.HasFlag(XMichelsonContractTags.Ledger))
                    {
                        // there must be only one ledger bigmap
                        bigmap.Tags &= ~BigMapTag.LedgerMask;
                        Logger.LogWarning("Multiple ledger bigmaps discovered for {contract}", contract.Hash);
                    }
                    else
                    {
                        Db.TryAttach(contract);
                        contract.Tags |= XMichelsonContractTags.Ledger;
                        if ((bigmap.Tags & BigMapTag.LedgerNft) != 0)
                            contract.Tags |= XMichelsonContractTags.Nft;
                    }
                }
            }
            else if (update.Action == BigMapAction.Remove)
            {
                if ((bigmap.Tags & BigMapTag.LedgerTypes) != 0)
                {
                    var contract = (await Cache.Addresses.GetAsync(
                        op is TransactionOperation tx
                            ? tx.TargetId
                            : (op as OriginationOperation)!.ContractId!.Value
                    ) as XMichelsonContract)!;

                    Db.TryAttach(contract);
                    contract.Tags &= ~XMichelsonContractTags.Ledger;
                    if ((bigmap.Tags & BigMapTag.LedgerNft) != 0)
                        contract.Tags &= ~XMichelsonContractTags.Nft;
                }
            }
            else if ((bigmap.Tags & (BigMapTag.Persistent | BigMapTag.Ledger)) == BigMapTag.Persistent &&
                op is TransactionOperation tx && tx.Entrypoint == "transfer" && await Cache.Addresses.GetAsync(tx.TargetId) is XMichelsonContract contract &&
                (contract.Tags & (XMichelsonContractTags.FA | XMichelsonContractTags.Ledger)) == XMichelsonContractTags.FA)
            {
                Db.TryAttach(bigmap);
                bigmap.Tags |= BigMaps.GetLedgerType(bigmap.Schema);

                if (bigmap.Tags.HasFlag(BigMapTag.Ledger))
                {
                    Db.TryAttach(contract);
                    contract.Tags |= XMichelsonContractTags.Ledger;
                    if ((bigmap.Tags & BigMapTag.LedgerNft) != 0)
                        contract.Tags |= XMichelsonContractTags.Nft;

                    pendingBigMaps ??= [];
                    pendingBigMaps.Add(bigmap.Id, bigmap);
                }
                else
                {
                    bigmap.Tags |= BigMapTag.Ledger;
                    Logger.LogWarning("Unsupported ledger bigmap #{ptr} ignored", bigmap.Ptr);
                }
            }
        }
        if (pendingBigMaps != null)
        {
            #region load entities
            var ids = pendingBigMaps.Keys.ToHashSet();
            var pendingUpdates = await Db.BigMapUpdates
                .AsNoTracking()
                .Where(x => ids.Contains(x.BigMapId) &&
                            x.Action != BigMapAction.Allocate &&
                            x.Level < block.Level)
                .OrderBy(x => x.Id)
                .ToListAsync();

            // TODO: remove this
            pendingUpdates.AddRange(Db.ChangeTracker.Entries()
                .Where(x => x.State == EntityState.Added && x.Entity is BigMapUpdate bu &&
                    ids.Contains(bu.BigMapId) &&
                    bu.Action != BigMapAction.Allocate &&
                    bu.Level < block.Level)
                .Select(x => x.Entity)
                .OfType<BigMapUpdate>()
                .OrderBy(x => x.Id));

            var keys = pendingUpdates.Count == 0 ? [] : await Db.BigMapKeys
                .AsNoTracking()
                .Where(x => ids.Contains(x.BigMapId))
                .ToDictionaryAsync(x => x.Id);

            // TODO: remove this
            var pendingKeys = Db.ChangeTracker.Entries()
                .Where(x => x.State == EntityState.Added && x.Entity is BigMapKey bk &&
                    ids.Contains(bk.BigMapId))
                .Select(x => x.Entity)
                .OfType<BigMapKey>();

            foreach (var key in pendingKeys)
                keys.Add(key.Id, key);

            var txIds = pendingUpdates
                .Where(x => x.TransactionId != null)
                .Select(x => x.TransactionId!.Value)
                .ToHashSet();

            var txs = txIds.Count == 0 ? [] : await Db.TransactionOps
                //.AsNoTracking()
                .Where(x => txIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => (x as IBigmapOperation)!);

            // transactions from earlier blocks of the current batch are not yet in the DB
            foreach (var tx in Batch.TransactionOps.Where(x => txIds.Contains(x.Id)))
                txs.Add(tx.Id, (tx as IBigmapOperation)!);

            var origIds = pendingUpdates
                .Where(x => x.OriginationId != null)
                .Select(x => x.OriginationId!.Value)
                .ToHashSet();

            var origs = origIds.Count == 0 ? [] : await Db.OriginationOps
                //.AsNoTracking()
                .OfType<XMichelsonOriginationOperation>()
                .Where(x => origIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            // TODO: remove this
            var pendingOrigs = Db.ChangeTracker.Entries()
                .Where(x => x.State == EntityState.Added && x.Entity is XMichelsonOriginationOperation orig &&
                    origIds.Contains(orig.Id))
                .Select(x => x.Entity)
                .OfType<XMichelsonOriginationOperation>();

            foreach (var orig in pendingOrigs)
                origs.Add(orig.Id, orig);

            var contracts = pendingBigMaps.Values
                .Select(x => x.ContractId)
                .ToHashSet();

            // transfers with no balance updates, e.g. to itself or with 0 amount
            var pendingTransfers = await Db.TransactionOps
                //.AsNoTracking()
                .Where(x => contracts.Contains(x.TargetId) &&
                            x.Status == OperationStatus.Applied &&
                            x.Entrypoint == "transfer" &&
                            x.TokenTransfers == null &&
                            x.Level < block.Level)
                .OrderBy(x => x.Id)
                .ToListAsync();

            // transactions from earlier blocks of the current batch are not yet in the DB
            pendingTransfers.AddRange(Batch.TransactionOps
                .Where(x => contracts.Contains(x.TargetId) &&
                            x.Status == OperationStatus.Applied &&
                            x.Entrypoint == "transfer" &&
                            x.TokenTransfers == null &&
                            x.Level < block.Level)
                .OrderBy(x => x.Id));
            #endregion

            #region preload
            var blocks = pendingTransfers.Select(x => x.Level)
                .Concat(pendingUpdates.Select(x => x.Level))
                .ToHashSet();

            var targets = pendingTransfers.Select(x => x.TargetId)
                .Concat(pendingBigMaps.Select(x => x.Value.ContractId))
                .ToHashSet();

            await Cache.Blocks.Preload(blocks);
            await Cache.Addresses.Preload(targets);
            #endregion

            #region group
            foreach (var tx in pendingTransfers)
            {
                if (!opBlocks.ContainsKey(tx.Level))
                {
                    var opBlock = Cache.Blocks.GetCached(tx.Level);
                    opBlocks.Add(tx.Level, opBlock);
                    if (!Batch.Contains(opBlock)) Db.TryAttach(opBlock);
                }

                var tokens = new Dictionary<BigInteger, (
                    List<(string From, byte[]? FromEp, string To, byte[]? ToEp, BigInteger Amount)> Transfers,
                    List<(string Address, byte[]? Ep, BigInteger Balance)> Balances
                )>();

                foreach (var (from, fromEp, to, toEp, tokenId, amount) in ParseTransferParam(Micheline.FromBytes(((tx as XMichelsonTransactionOperation)?.ParametersRaw ?? (tx as XEvmMichelsonTransactionOperation)!.ParametersRaw)!)))
                {
                    if (!tokens.TryGetValue(tokenId, out var ctx))
                    {
                        ctx = ([], []);
                        tokens.Add(tokenId, ctx);
                    }
                    ctx.Transfers.Add((from, fromEp, to, toEp, amount));
                }

                var contract = (Cache.Addresses.GetCached(tx.TargetId) as XMichelsonContract)!;
                ops.Add((tx as IBigmapOperation)!, (false, contract, tokens));
            }

            foreach (var update in pendingUpdates)
            {
                var bigmap = pendingBigMaps[update.BigMapId];
                var op = update.OriginationId != null
                    ? origs[update.OriginationId.Value]
                    : txs[update.TransactionId!.Value];

                if (!opBlocks.ContainsKey(op.Level))
                {
                    var opBlock = Cache.Blocks.GetCached(op.Level);
                    opBlocks.Add(op.Level, opBlock);
                    if (!Batch.Contains(opBlock)) Db.TryAttach(opBlock);
                }

                if (!ops.TryGetValue(op, out var opCtx))
                {
                    var contract = (Cache.Addresses.GetCached(bigmap.ContractId) as XMichelsonContract)!;
                    opCtx = (false, contract, []);
                    ops.Add(op, opCtx);
                }

                if (update.Action == BigMapAction.Remove)
                {
                    ops[op] = (true, ops[op].Contract, ops[op].Tokens);
                }
                else
                {
                    var key = keys[update.BigMapKeyId!.Value];

                    foreach (var (address, ep, tokenId, balance) in BigMaps.ParseLedger(bigmap, key, update))
                    {
                        if (!opCtx.Tokens.TryGetValue(tokenId, out var tokenCtx))
                        {
                            tokenCtx = ([], []);
                            opCtx.Tokens.Add(tokenId, tokenCtx);
                        }
                        tokenCtx.Balances.Add((address, ep, balance));
                    }
                }

            }
            #endregion
        }
        #endregion

        #region group updates
        foreach (var tx in Context.TransactionOps.Where(x => x is XMichelsonTransactionOperation or XEvmMichelsonTransactionOperation))
        {
            if (tx.Status == OperationStatus.Applied && tx.Entrypoint == "transfer")
            {
                var contract = (await Cache.Addresses.GetAsync(tx.TargetId) as XMichelsonContract)!;
                if (contract.Tags.HasFlag(XMichelsonContractTags.Ledger))
                {
                    var tokens = new Dictionary<BigInteger, (
                        List<(string From, byte[]? FromEp, string To, byte[]? ToEp, BigInteger Amount)> Transfers,
                        List<(string Address, byte[]? Ep, BigInteger Balance)> Balances
                    )>();

                    foreach (var (from, fromEp, to, toEp, tokenId, amount) in ParseTransferParam(Micheline.FromBytes(((tx as XMichelsonTransactionOperation)?.ParametersRaw ?? (tx as XEvmMichelsonTransactionOperation)!.ParametersRaw)!)))
                    {
                        if (!tokens.TryGetValue(tokenId, out var ctx))
                        {
                            ctx = ([], []);
                            tokens.Add(tokenId, ctx);
                        }
                        ctx.Transfers.Add((from, fromEp, to, toEp, amount));
                    }

                    ops.Add((tx as IBigmapOperation)!, (false, contract, tokens));
                    //if (!opBlocks.ContainsKey(tx.Level))
                    //{
                    //    var opBlock = Cache.Blocks.GetCached(tx.Level);
                    //    opBlocks.Add(tx.Level, opBlock);
                    //    Db.TryAttach(opBlock);
                    //}
                }
            }
        }
        foreach (var (bigmap, key, update, op) in updates)
        {
            if ((bigmap.Tags & BigMapTag.LedgerTypes) == 0 || update.Action == BigMapAction.Allocate)
                continue;

            if (!ops.TryGetValue(op, out var opCtx))
            {
                var contract = (await Cache.Addresses.GetAsync(
                    op is TransactionOperation tx
                        ? tx.TargetId
                        : (op as OriginationOperation)!.ContractId!.Value
                ) as XMichelsonContract)!;

                opCtx = (false, contract, []);
                ops.Add(op, opCtx);
            }

            if (update.Action == BigMapAction.Remove)
            {
                ops[op] = (true, ops[op].Contract, ops[op].Tokens);
            }
            else
            {
                foreach (var (address, ep, tokenId, balance) in BigMaps.ParseLedger(bigmap, key!, update))
                {
                    if (!opCtx.Tokens.TryGetValue(tokenId, out var tokenCtx))
                    {
                        tokenCtx = ([], []);
                        opCtx.Tokens.Add(tokenId, tokenCtx);
                    }
                    tokenCtx.Balances.Add((address, ep, balance));
                }
            }
        }
        #endregion

        if (ops.Count == 0) return;

        #region precache
        var addressesSet = new HashSet<string>();
        var tokensSet = new HashSet<(int, BigInteger)>();
        var balancesSet = new HashSet<(int, HashKey?, long)>();
        var nftAddressesSet = new HashSet<int>();

        foreach (var (op, opCtx) in ops)
        {
            foreach (var (tokenId, tokenCtx) in opCtx.Tokens)
            {
                foreach (var (from, _, to, _, _) in tokenCtx.Transfers)
                {
                    addressesSet.Add(from);
                    addressesSet.Add(to);
                    tokensSet.Add((opCtx.Contract.Id, tokenId));
                }
                foreach (var (address, _, _) in tokenCtx.Balances)
                {
                    addressesSet.Add(address);
                    tokensSet.Add((opCtx.Contract.Id, tokenId));
                }
            }
        }

        await Cache.Tokens.Preload(tokensSet);
        await Cache.Addresses.Preload(addressesSet);

        foreach (var (op, opCtx) in ops)
        {
            foreach (var (tokenId, tokenCtx) in opCtx.Tokens)
            {
                foreach (var (from, fromEp, to, toEp, _) in tokenCtx.Transfers)
                    if (Cache.Tokens.TryGet(opCtx.Contract.Id, tokenId, out var token))
                    {
                        if (Cache.Addresses.TryGetCached(from, out var fromAcc))
                            balancesSet.Add((fromAcc.Id, HashKey.From(fromEp), token.Id));

                        if (Cache.Addresses.TryGetCached(to, out var toAcc))
                            balancesSet.Add((toAcc.Id, HashKey.From(toEp), token.Id));
                    }

                foreach (var (address, ep, _) in tokenCtx.Balances)
                    if (Cache.Tokens.TryGet(opCtx.Contract.Id, tokenId, out var token))
                    {
                        if (Cache.Addresses.TryGetCached(address, out var acc))
                            balancesSet.Add((acc.Id, HashKey.From(ep), token.Id));

                        if (token.OwnerId != null)
                        {
                            nftAddressesSet.Add(token.OwnerId.Value);
                            balancesSet.Add((token.OwnerId.Value, HashKey.From(token.OwnerEntrypoint), token.Id));
                        }
                    }
            }
        }

        await Cache.Addresses.Preload(nftAddressesSet);
        await Cache.TokenBalances.Preload(balancesSet);
        #endregion

        foreach (var (op, opCtx) in ops.OrderBy(kv => kv.Key.Id))
        {
            if (opCtx.Reset)
            {
                opBlocks[op.Level].Events |= XBlockEvents.Tokens;
                await ResetLedgers(op, opCtx.Contract);
            }

            foreach (var (tokenId, tokenCtx) in opCtx.Tokens)
            {
                if (Cache.Tokens.TryGet(opCtx.Contract.Id, tokenId, out var token))
                {
                    if (token.OwnerId != null && tokenCtx.Balances.Count == 1 && tokenCtx.Balances[0].Balance != BigInteger.Zero)
                    {
                        var prevHolder = Cache.Addresses.GetCached((int)token.OwnerId);
                        if (prevHolder.Hash != tokenCtx.Balances[0].Address)
                            tokenCtx.Balances.Add((prevHolder.Hash, token.OwnerEntrypoint, BigInteger.Zero));
                    }

                    if (tokenCtx.Transfers.Count > 0 && ValidateTransfers(token, tokenCtx))
                    {
                        await ProcessTransfers(op, opBlocks[op.Level], opCtx.Contract, token, tokenCtx.Transfers);
                    }
                    else
                    {
                        var diffs = await GetDiffs(op, opBlocks[op.Level], token, tokenCtx.Balances);
                        if (diffs.Count > 0)
                        {
                            ProcessDiffs(op, opBlocks[op.Level], opCtx.Contract, token, diffs);
                        }
                    }
                }
                else
                {
                    if (tokenCtx.Transfers.Count > 0 && ValidateTransfers(tokenCtx))
                    {
                        token = GetOrCreateToken(op, opBlocks[op.Level], opCtx.Contract, tokenId);
                        await ProcessTransfers(op, opBlocks[op.Level], opCtx.Contract, token, tokenCtx.Transfers);
                    }
                    else
                    {
                        var diffs = await GetDiffs(op, opBlocks[op.Level], opCtx.Contract, tokenId, tokenCtx.Balances);
                        if (diffs.Count > 0)
                        {
                            token = GetOrCreateToken(op, opBlocks[op.Level], opCtx.Contract, tokenId);
                            ProcessDiffs(op, opBlocks[op.Level], opCtx.Contract, token, diffs);
                        }
                    }
                }
            }
        }
    }

    async Task ResetLedgers(IBigmapOperation op, XMichelsonContract contract)
    {
        var tokens = await Db.Tokens
            //.AsNoTracking()
            .Where(x => x.ContractId == contract.Id)
            .ToListAsync();
        var tokenIds = tokens.Select(x => x.Id).ToHashSet();

        foreach (var token in tokens)
            Cache.Tokens.Add(token);

        var tokenBalances = await Db.TokenBalances
            .AsNoTracking()
            .Where(x => tokenIds.Contains(x.TokenId))
            .ToListAsync();

        var addressIds = tokenBalances.Select(x => x.AddressId).ToHashSet();
        await Cache.Addresses.Preload(addressIds);

        foreach (var tb in tokenBalances)
        {
            var tokenBalance = Cache.TokenBalances.GetOrAdd(tb);
            if (tokenBalance.Balance == BigInteger.Zero) continue;
            var address = (Cache.Addresses.GetCached(tokenBalance.AddressId) as XMichelsonAddress)!;
            var token = Cache.Tokens.Get(tokenBalance.TokenId);
            token.LastLevel = op.Level;
            token.LastTimestamp = op.Timestamp;
            MintOrBurnTokens(op, contract, token, address, tokenBalance, -tokenBalance.Balance);
        }
    }

    static bool ValidateTransfers((List<(string, byte[]?, string, byte[]?, BigInteger)> Transfers, List<(string, byte[]?, BigInteger)> Balances) ctx)
    {
        var dic = new Dictionary<(string, HashKey?), BigInteger>();
        foreach (var (from, fromEp, to, toEp, amount) in ctx.Transfers)
        {
            if (!dic.ContainsKey((from, HashKey.From(fromEp))))
                dic.Add((from, HashKey.From(fromEp)), BigInteger.Zero);

            if (!dic.ContainsKey((to, HashKey.From(toEp))))
                dic.Add((to, HashKey.From(toEp)), BigInteger.Zero);

            dic[(from, HashKey.From(fromEp))] -= amount;
            dic[(to, HashKey.From(toEp))] += amount;
        }
        foreach (var (address, ep, balance) in ctx.Balances)
        {
            if (balance != BigInteger.Zero)
            {
                if (!dic.ContainsKey((address, HashKey.From(ep))))
                    return false;

                dic[(address, HashKey.From(ep))] -= balance;
            }
        }
        return dic.Values.All(x => x == BigInteger.Zero);
    }

    bool ValidateTransfers(Token token, (List<(string, byte[]?, string, byte[]?, BigInteger)> Transfers, List<(string, byte[]?, BigInteger)> Balances) ctx)
    {
        var dic = new Dictionary<(string, HashKey?), BigInteger>();
        foreach (var (from, fromEp, to, toEp, amount) in ctx.Transfers)
        {
            if (!dic.ContainsKey((from, HashKey.From(fromEp))))
                dic.Add((from, HashKey.From(fromEp)), BigInteger.Zero);

            if (!dic.ContainsKey((to, HashKey.From(toEp))))
                dic.Add((to, HashKey.From(toEp)), BigInteger.Zero);

            dic[(from, HashKey.From(fromEp))] -= amount;
            dic[(to, HashKey.From(toEp))] += amount;
        }
        foreach (var (addressHash, ep, balance) in ctx.Balances)
        {
            var prevBalance = BigInteger.Zero;
            if (Cache.Addresses.TryGetCached(addressHash, out var address) &&
                Cache.TokenBalances.TryGet(address.Id, ep, token.Id, out var tokenBalance))
                prevBalance = tokenBalance.Balance;

            var diff = balance - prevBalance;
            if (diff != BigInteger.Zero)
            {
                if (!dic.ContainsKey((addressHash, HashKey.From(ep))))
                    return false;

                dic[(addressHash, HashKey.From(ep))] -= diff;
            }
        }
        return dic.Values.All(x => x == BigInteger.Zero);
    }

    async Task<List<(XMichelsonAddress, TokenBalance, BigInteger)>> GetDiffs(IBigmapOperation op, XBlock block, XMichelsonContract contract, BigInteger tokenId, List<(string, byte[]?, BigInteger)> balances)
    {
        var diffs = new List<(XMichelsonAddress, TokenBalance, BigInteger Diff)>(balances.Count);
        foreach (var (addressHash, ep, balance) in balances)
        {
            if (balance != BigInteger.Zero)
            {
                var token = GetOrCreateToken(op, block, contract, tokenId);
                var address = await Helpers.GetCachedOrCreateXMichelsonAddress(addressHash, block);
                var tokenBalance = GetOrCreateTokenBalance(op, block, token, address, ep);
                diffs.Add((address, tokenBalance, balance));
            }
        }
        return diffs;
    }

    async Task<List<(XMichelsonAddress, TokenBalance, BigInteger)>> GetDiffs(IBigmapOperation op, XBlock block, Token token, List<(string, byte[]?, BigInteger)> balances)
    {
        var diffs = new List<(XMichelsonAddress, TokenBalance, BigInteger Diff)>(balances.Count);
        foreach (var (addressHash, ep, balance) in balances)
        {
            var prevBalance = BigInteger.Zero;
            if (Cache.Addresses.TryGetCached(addressHash, out var address) &&
                Cache.TokenBalances.TryGet(address.Id, ep, token.Id, out var tokenBalance))
                prevBalance = tokenBalance.Balance;

            var diff = balance - prevBalance;
            if (diff != BigInteger.Zero)
            {
                address = await Helpers.GetCachedOrCreateXMichelsonAddress(addressHash, block);
                tokenBalance = GetOrCreateTokenBalance(op, block, token, (address as XMichelsonAddress)!, ep);
                diffs.Add(((address as XMichelsonAddress)!, tokenBalance, diff));
            }
        }
        return diffs;
    }

    async Task ProcessTransfers(IBigmapOperation op, XBlock block, XMichelsonContract contract, Token token, List<(string, byte[]?, string, byte[]?, BigInteger)> transfers)
    {
        Db.TryAttach(token);
        token.LastLevel = op.Level;
        token.LastTimestamp = op.Timestamp;

        block.Events |= XBlockEvents.Tokens;

        foreach (var (from, fromEp, to, toEp, amount) in transfers)
        {
            var fromAcc = await Helpers.GetCachedOrCreateXMichelsonAddress(from, block);
            var fromBalance = GetOrCreateTokenBalance(op, block, token, fromAcc, fromEp);
            var toAcc = await Helpers.GetCachedOrCreateXMichelsonAddress(to, block);
            var toBalance = GetOrCreateTokenBalance(op, block, token, toAcc, toEp);
            TransferTokens(op, contract, token, fromAcc, fromBalance, toAcc, toBalance, amount);
        }
    }

    void ProcessDiffs(IBigmapOperation op, XBlock block, XMichelsonContract contract, Token token, List<(XMichelsonAddress, TokenBalance, BigInteger Diff)> diffs)
    {
        Db.TryAttach(token);
        token.LastLevel = op.Level;
        token.LastTimestamp = op.Timestamp;

        block.Events |= XBlockEvents.Tokens;

        if (diffs.Count == 1 || diffs.BigSum(x => x.Diff) != BigInteger.Zero)
        {
            foreach (var (address, tokenBalance, diff) in diffs)
                MintOrBurnTokens(op, contract, token, address, tokenBalance, diff);
        }
        else if (diffs.Count(x => x.Diff < BigInteger.Zero) == 1)
        {
            var (fromAcc, fromBalance, fromDiff) = diffs.First(x => x.Diff < BigInteger.Zero);
            foreach (var (toAcc, toBalance, toDiff) in diffs)
            {
                if (toBalance == fromBalance) continue;
                TransferTokens(op, contract, token, fromAcc, fromBalance, toAcc, toBalance, toDiff);
            }
        }
        else if (diffs.Count(x => x.Diff > BigInteger.Zero) == 1)
        {
            var (toAcc, toBalance, toDiff) = diffs.First(x => x.Diff > BigInteger.Zero);
            foreach (var (fromAcc, fromBalance, fromDiff) in diffs)
            {
                if (fromBalance == toBalance) continue;
                TransferTokens(op, contract, token, fromAcc, fromBalance, toAcc, toBalance, -fromDiff);
            }
        }
        else
        {
            foreach (var (address, tokenBalance, diff) in diffs)
                MintOrBurnTokens(op, contract, token, address, tokenBalance, diff);
        }
    }

    Token GetOrCreateToken(IBigmapOperation op, XBlock block, XMichelsonContract contract, BigInteger tokenId)
    {
        if (!Cache.Tokens.TryGet(contract.Id, tokenId, out var token))
        {
            var state = Cache.Chain.Get();
            state.TokensCount++;

            token = new Token
            {
                Id = Cache.Chain.NextSubId(op),
                ChainId = op.ChainId,
                ContractId = contract.Id,
                TokenId = tokenId,
                FirstMinterId = op.InitiatorId ?? op.SenderId,
                FirstLevel = op.Level,
                FirstTimestamp = op.Timestamp,
                LastLevel = op.Level,
                LastTimestamp = op.Timestamp,
                TotalBurned = BigInteger.Zero,
                TotalMinted = BigInteger.Zero,
                TotalSupply = BigInteger.Zero,
                Tags = contract.Tags.HasFlag(XMichelsonContractTags.Nft)
                    ? TokenTags.FaNft
                    : contract.Tags.HasFlag(XMichelsonContractTags.FA2)
                        ? TokenTags.Fa2
                        : TokenTags.Fa12,
                IndexedAt = op.Level <= state.Level ? state.Level + 1 : null
            };
            Db.Tokens.Add(token);
            Cache.Tokens.Add(token);

            Db.TryAttach(contract);
            contract.TokensCount++;
            contract.LastLevel = Math.Max(contract.LastLevel, op.Level);
            contract.LastTimestamp = contract.LastTimestamp > op.Timestamp ? contract.LastTimestamp : op.Timestamp;

            if (!Batch.Contains(block)) Db.TryAttach(block);
            block.Events |= XBlockEvents.Tokens;
        }
        return token;
    }

    TokenBalance GetOrCreateTokenBalance(IBigmapOperation op, XBlock block, Token token, XMichelsonAddress address, byte[]? entrypoint)
    {
        if (!Cache.TokenBalances.TryGet(address.Id, entrypoint, token.Id, out var tokenBalance))
        {
            var state = Cache.Chain.Get();
            state.TokenBalancesCount++;

            tokenBalance = new TokenBalance
            {
                Id = Cache.Chain.NextSubId(op),
                ChainId = op.ChainId,
                AddressId = address.Id,
                Entrypoint = entrypoint,
                TokenId = token.Id,
                ContractId = token.ContractId,
                FirstLevel = op.Level,
                FirstTimestamp = op.Timestamp,
                LastLevel = op.Level,
                LastTimestamp = op.Timestamp,
                Balance = BigInteger.Zero,
                IndexedAt = op.Level <= state.Level ? state.Level + 1 : null
            };
            Db.TokenBalances.Add(tokenBalance);
            Cache.TokenBalances.Add(tokenBalance);

            Db.TryAttach(token);
            token.BalancesCount++;

            Db.TryAttach(address);
            address.TokenBalancesCount++;
            address.LastLevel = Math.Max(address.LastLevel, op.Level);
            address.LastTimestamp = address.LastTimestamp > op.Timestamp ? address.LastTimestamp : op.Timestamp;
            if (address.FirstLevel > op.Level)
            {
                address.FirstLevel = op.Level;
                address.FirstTimestamp = op.Timestamp;
                block.Events |= XBlockEvents.NewAddresses;
            }
        }
        return tokenBalance;
    }

    void TransferTokens(IBigmapOperation op, XMichelsonContract contract, Token token,
        XMichelsonAddress from, TokenBalance fromBalance,
        XMichelsonAddress to, TokenBalance toBalance,
        BigInteger amount)
    {
        op.TokenTransfers = (op.TokenTransfers ?? 0) + 1;

        Db.TryAttach(from);
        from.TokenTransfersCount++;
        from.LastLevel = Math.Max(from.LastLevel, op.Level);
        from.LastTimestamp = from.LastTimestamp > op.Timestamp ? from.LastTimestamp : op.Timestamp;

        if (to != from)
        {
            Db.TryAttach(to);
            to.TokenTransfersCount++;
            to.LastLevel = Math.Max(to.LastLevel, op.Level);
            to.LastTimestamp = to.LastTimestamp > op.Timestamp ? to.LastTimestamp : op.Timestamp;
        }

        Db.TryAttach(fromBalance);
        fromBalance.Balance -= amount;
        fromBalance.TransfersCount++;
        fromBalance.LastLevel = op.Level;
        fromBalance.LastTimestamp = op.Timestamp;

        Db.TryAttach(toBalance);
        toBalance.Balance += amount;
        if (toBalance != fromBalance) toBalance.TransfersCount++;
        toBalance.LastLevel = op.Level;
        toBalance.LastTimestamp = op.Timestamp;

        token.TransfersCount++;
        if (amount != BigInteger.Zero && fromBalance.Id != toBalance.Id)
        {
            if (fromBalance.Balance == BigInteger.Zero)
            {
                from.ActiveTokensCount--;
                token.HoldersCount--;
            }
            if (toBalance.Balance == amount)
            {
                to.ActiveTokensCount++;
                token.HoldersCount++;
            }
            if (contract.Tags.HasFlag(XMichelsonContractTags.Nft))
            {
                token.OwnerId = to.Id;
                token.OwnerEntrypoint = toBalance.Entrypoint;
            }
        }

        var state = Cache.Chain.Get();
        state.TokenTransfersCount++;

        Batch.TokenTransfers.Add(new TokenTransfer
        {
            Id = Cache.Chain.NextSubId(op),
            ChainId = op.ChainId,
            Amount = amount,
            FromId = from.Id,
            FromEntrypoint = fromBalance.Entrypoint,
            ToId = to.Id,
            ToEntrypoint = toBalance.Entrypoint,
            Level = op.Level,
            Timestamp = op.Timestamp,
            TokenId = token.Id,
            ContractId = token.ContractId,
            TransactionId = (op as TransactionOperation)?.Id,
            OriginationId = (op as OriginationOperation)?.Id,
            IndexedAt = op.Level <= state.Level ? state.Level + 1 : null
        });
    }

    void MintOrBurnTokens(IBigmapOperation op, XMichelsonContract contract, Token token,
        XMichelsonAddress address, TokenBalance balance,
        BigInteger diff)
    {
        op.TokenTransfers = (op.TokenTransfers ?? 0) + 1;

        Db.TryAttach(address);
        address.TokenTransfersCount++;
        address.LastLevel = Math.Max(address.LastLevel, op.Level);
        address.LastTimestamp = address.LastTimestamp > op.Timestamp ? address.LastTimestamp : op.Timestamp;

        Db.TryAttach(balance);
        balance.Balance += diff;
        balance.TransfersCount++;
        balance.LastLevel = op.Level;
        balance.LastTimestamp = op.Timestamp;

        token.TransfersCount++;
        if (balance.Balance == BigInteger.Zero)
        {
            address.ActiveTokensCount--;
            token.HoldersCount--;

            if (contract.Tags.HasFlag(XMichelsonContractTags.Nft))
            {
                token.OwnerId = null;
                token.OwnerEntrypoint = null;
            }
        }
        if (balance.Balance == diff)
        {
            address.ActiveTokensCount++;
            token.HoldersCount++;

            if (contract.Tags.HasFlag(XMichelsonContractTags.Nft))
            {
                token.OwnerId = address.Id;
                token.OwnerEntrypoint = balance.Entrypoint;
            }
        }
        if (diff > 0) token.TotalMinted += diff;
        else token.TotalBurned += -diff;
        token.TotalSupply += diff;

        var state = Cache.Chain.Get();
        state.TokenTransfersCount++;

        Batch.TokenTransfers.Add(new TokenTransfer
        {
            Id = Cache.Chain.NextSubId(op),
            ChainId = op.ChainId,
            Amount = diff > BigInteger.Zero ? diff : -diff,
            FromId = diff < BigInteger.Zero ? address.Id : null,
            FromEntrypoint = diff < BigInteger.Zero ? balance.Entrypoint : null,
            ToId = diff > BigInteger.Zero ? address.Id : null,
            ToEntrypoint = diff > BigInteger.Zero ? balance.Entrypoint : null,
            Level = op.Level,
            Timestamp = op.Timestamp,
            TokenId = token.Id,
            ContractId = token.ContractId,
            TransactionId = (op as TransactionOperation)?.Id,
            OriginationId = (op as OriginationOperation)?.Id,
            IndexedAt = op.Level <= state.Level ? state.Level + 1 : null
        });
    }

    static List<(string, byte[]?, string, byte[]?, BigInteger, BigInteger)> ParseTransferParam(IMicheline micheline)
    {
        var transfers = new List<(string, byte[]?, string, byte[]?, BigInteger, BigInteger)>();
        if (micheline is MichelineArray arr)
        {
            foreach (var transfer in arr)
            {
                var transferPair = (transfer as MichelinePrim)!;
                var (from, fromEp) = transferPair.Args![0].ParseAddressWithEntrypoint();
                foreach (var tx in (transferPair.Args[1] as MichelineArray)!)
                {
                    var txPair = (tx as MichelinePrim)!;
                    var (to, toEp) = txPair.Args![0].ParseAddressWithEntrypoint();
                    var txPair2 = (txPair.Args[1] as MichelinePrim)!;
                    var tokenId = (txPair2.Args![0] as MichelineInt)!.Value;
                    var amount = (txPair2.Args[1] as MichelineInt)!.Value;

                    transfers.Add((from, fromEp, to, toEp, tokenId, amount));
                }
            }
        }
        else if (micheline is MichelinePrim pair)
        {
            var (from, fromEp) = pair.Args![0].ParseAddressWithEntrypoint();
            var pair2 = (pair.Args[1] as MichelinePrim)!;
            var (to, toEp) = pair2.Args![0].ParseAddressWithEntrypoint();
            var value = (pair2.Args[1] as MichelineInt)!.Value;

            transfers.Add((from, fromEp, to, toEp, BigInteger.Zero, value));
        }
        return transfers;
    }
}
