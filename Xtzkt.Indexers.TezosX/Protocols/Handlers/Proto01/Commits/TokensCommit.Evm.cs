using System.Numerics;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01
{
    partial class TokensCommit
    {
        public virtual async Task ApplyEvmTransfers()
        {
            if (Context.EvmTokenTransfers.Count == 0)
                return;

            #region precache
            var tokensSet = new HashSet<(int, BigInteger)>();
            var addressesSet = new HashSet<string>();

            foreach (var tr in Context.EvmTokenTransfers)
            {
                tokensSet.Add((tr.Contract.Id, tr.TokenId));
                
                if (tr.From != EvmRuntime.NullAddress)
                    addressesSet.Add(tr.From);
                
                if (tr.To != EvmRuntime.NullAddress)
                    addressesSet.Add(tr.To);
            }

            await Cache.Tokens.Preload(tokensSet);
            await Cache.Addresses.Preload(addressesSet);

            var balancesSet = new HashSet<(int, HashableBytes?, long)>();
            foreach (var tr in Context.EvmTokenTransfers)
            {
                if (!Cache.Tokens.TryGet(tr.Contract.Id, tr.TokenId, out var token))
                    continue;

                if (tr.From != EvmRuntime.NullAddress && Cache.Addresses.TryGetCached(tr.From, out var fromAcc))
                    balancesSet.Add((fromAcc.Id, null, token.Id));

                if (tr.To != EvmRuntime.NullAddress && Cache.Addresses.TryGetCached(tr.To, out var toAcc))
                    balancesSet.Add((toAcc.Id, null, token.Id));
            }

            await Cache.TokenBalances.Preload(balancesSet);
            #endregion

            foreach (var tr in Context.EvmTokenTransfers)
            {
                var isMint = tr.From == EvmRuntime.NullAddress;
                var isBurn = tr.To == EvmRuntime.NullAddress;
                if (isMint && isBurn)
                    continue;

                var op = tr.Op;
                Context.Block.Events |= XBlockEvents.Tokens;

                var token = GetOrCreateEvmToken(op, tr.Contract, tr.TokenId, tr.Type);
                Db.TryAttach(token);
                token.LastLevel = op.Level;
                token.LastTimestamp = op.Timestamp;

                if (isMint)
                {
                    var to = await GetCachedOrCreateXAddress(tr.To);
                    var toBalance = GetOrCreateEvmTokenBalance(op, token, to);
                    MintOrBurnEvmTokens(op, token, to, toBalance, tr.Amount);
                }
                else if (isBurn)
                {
                    var from = await GetCachedOrCreateXAddress(tr.From);
                    var fromBalance = GetOrCreateEvmTokenBalance(op, token, from);
                    MintOrBurnEvmTokens(op, token, from, fromBalance, -tr.Amount);
                }
                else
                {
                    var from = await GetCachedOrCreateXAddress(tr.From);
                    var fromBalance = GetOrCreateEvmTokenBalance(op, token, from);
                    var to = await GetCachedOrCreateXAddress(tr.To);
                    var toBalance = GetOrCreateEvmTokenBalance(op, token, to);
                    TransferEvmTokens(op, token, from, fromBalance, to, toBalance, tr.Amount);
                }
            }
        }

        async Task<XAddress> GetCachedOrCreateXAddress(string hash)
        {
            if (!Cache.Addresses.TryGetCached(hash, out var address))
                address = await CreateXEvmUser(hash);
            return address;
        }

        Token GetOrCreateEvmToken(ISourceOperation op, XEvmContract contract, BigInteger tokenId, TokenTags type)
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
                    FirstMinterId = op switch
                    {
                        TransactionOperation o => o.InitiatorId ?? o.SenderId,
                        OriginationOperation o => o.InitiatorId ?? o.SenderId,
                        _ => throw new InvalidOperationException("Invalid EVM token operation")
                    },
                    FirstLevel = op.Level,
                    FirstTimestamp = op.Timestamp,
                    LastLevel = op.Level,
                    LastTimestamp = op.Timestamp,
                    TotalBurned = BigInteger.Zero,
                    TotalMinted = BigInteger.Zero,
                    TotalSupply = BigInteger.Zero,
                    Tags = type,
                };
                Db.Tokens.Add(token);
                Cache.Tokens.Add(token);

                Db.TryAttach(contract);
                if (contract.TokensCount == 0)
                {
                    contract.Kind = XContractKind.Asset;
                    contract.Tags |= type switch
                    {
                        TokenTags.Erc20 => XEvmContractTags.ERC20,
                        TokenTags.Erc721 => XEvmContractTags.ERC721,
                        TokenTags.Erc1155 => XEvmContractTags.ERC1155,
                        _ => throw new InvalidOperationException("Invalid token type"),
                    };
                }
                contract.TokensCount++;
                contract.LastLevel = op.Level;
                contract.LastTimestamp = op.Timestamp;
            }
            return token;
        }

        TokenBalance GetOrCreateEvmTokenBalance(ISourceOperation op, Token token, XAddress address)
        {
            if (!Cache.TokenBalances.TryGet(address.Id, null, token.Id, out var tokenBalance))
            {
                var state = Cache.Chain.Get();
                state.TokenBalancesCount++;

                tokenBalance = new TokenBalance
                {
                    Id = Cache.Chain.NextSubId(op),
                    ChainId = op.ChainId,
                    AddressId = address.Id,
                    Entrypoint = null,
                    TokenId = token.Id,
                    ContractId = token.ContractId,
                    FirstLevel = op.Level,
                    FirstTimestamp = op.Timestamp,
                    LastLevel = op.Level,
                    LastTimestamp = op.Timestamp,
                    Balance = BigInteger.Zero,
                };
                Db.TokenBalances.Add(tokenBalance);
                Cache.TokenBalances.Add(tokenBalance);

                Db.TryAttach(token);
                token.BalancesCount++;

                Db.TryAttach(address);
                address.TokenBalancesCount++;
                address.LastLevel = op.Level;
                address.LastTimestamp = op.Timestamp;
            }
            return tokenBalance;
        }

        void TransferEvmTokens(ISourceOperation op, Token token,
            XAddress from, TokenBalance fromBalance,
            XAddress to, TokenBalance toBalance,
            BigInteger amount)
        {
            IncrementOpTransfers(op);

            Db.TryAttach(from);
            from.TokenTransfersCount++;
            from.LastLevel = op.Level;
            from.LastTimestamp = op.Timestamp;

            if (to != from)
            {
                Db.TryAttach(to);
                to.TokenTransfersCount++;
                to.LastLevel = op.Level;
                to.LastTimestamp = op.Timestamp;
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
                if (token.Tags.HasFlag(TokenTags.Erc721))
                {
                    token.OwnerId = to.Id;
                    token.OwnerEntrypoint = toBalance.Entrypoint;
                }
            }

            var state = Cache.Chain.Get();
            state.TokenTransfersCount++;

            Db.TokenTransfers.Add(new TokenTransfer
            {
                Id = Cache.Chain.NextSubId(op),
                ChainId = op.ChainId,
                Amount = amount,
                FromId = from.Id,
                FromEntrypoint = null,
                ToId = to.Id,
                ToEntrypoint = null,
                Level = op.Level,
                Timestamp = op.Timestamp,
                TokenId = token.Id,
                ContractId = token.ContractId,
                TransactionId = (op as TransactionOperation)?.Id,
                OriginationId = (op as OriginationOperation)?.Id,
            });
        }

        void MintOrBurnEvmTokens(ISourceOperation op, Token token,
            XAddress address, TokenBalance balance,
            BigInteger diff)
        {
            IncrementOpTransfers(op);

            Db.TryAttach(address);
            address.TokenTransfersCount++;
            address.LastLevel = op.Level;
            address.LastTimestamp = op.Timestamp;

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

                if (token.Tags.HasFlag(TokenTags.Erc721))
                {
                    token.OwnerId = null;
                    token.OwnerEntrypoint = null;
                }
            }
            if (balance.Balance == diff)
            {
                address.ActiveTokensCount++;
                token.HoldersCount++;

                if (token.Tags.HasFlag(TokenTags.Erc721))
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

            Db.TokenTransfers.Add(new TokenTransfer
            {
                Id = Cache.Chain.NextSubId(op),
                ChainId = op.ChainId,
                Amount = diff > BigInteger.Zero ? diff : -diff,
                FromId = diff < BigInteger.Zero ? address.Id : null,
                FromEntrypoint = null,
                ToId = diff > BigInteger.Zero ? address.Id : null,
                ToEntrypoint = null,
                Level = op.Level,
                Timestamp = op.Timestamp,
                TokenId = token.Id,
                ContractId = token.ContractId,
                TransactionId = (op as TransactionOperation)?.Id,
                OriginationId = (op as OriginationOperation)?.Id,
            });
        }

        static void IncrementOpTransfers(ISourceOperation op)
        {
            if (op is TransactionOperation t)
                t.TokenTransfers = (t.TokenTransfers ?? 0) + 1;
            else if (op is OriginationOperation o)
                o.TokenTransfers = (o.TokenTransfers ?? 0) + 1;
        }
    }
}
