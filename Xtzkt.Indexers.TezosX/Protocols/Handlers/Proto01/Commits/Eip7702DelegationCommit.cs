using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class Eip7702DelegationCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public async Task Apply(ISourceOperation op, XEvmAddress sender, JsonElement authorizations)
    {
        var _chainId = new BigInteger(Xtzkt.Utils.Encoding.Hex.GetBytes(Cache.Chain.Get().ChainId), true, true);
        foreach (var authorization in authorizations.EnumerateArray())
        {
            #region init
            XEvmUser authority;
            XEvmAddress? @delegate;
            Eip7702Delegation delegation;
            try
            {
                var (chainId, delegateAddress, nonce, authorityAddress) = Eip7702.ParseAuthorization(authorization);

                // zero chain id means the authorization is valid on any chain
                if (chainId != BigInteger.Zero && chainId != _chainId)
                    throw new Exception("Invalid 'chainId'");

                // the authority must not be created before the authorization is fully validated,
                // otherwise an invalid one would leave behind an empty account, which nothing removes
                var existingAuthority = await Cache.Addresses.GetOrDefaultAsync(authorityAddress);
                if (existingAuthority is not null and not XEvmUser)
                    throw new Exception($"Cannot interpret {existingAuthority.Type} as authority");

                // counter keeps the last used nonce, so the actual nonce of the account is Counter + 1,
                // and for an account that doesn't exist yet it's 0
                if (nonce != (ulong)(existingAuthority is XEvmUser existing ? existing.Counter + 1 : 0))
                    throw new Exception("Invalid 'nonce'");

                authority = existingAuthority as XEvmUser ?? await Helpers.CreateXEvmUser(authorityAddress);

                @delegate = delegateAddress == EvmRuntime.NullAddress ? null : await Helpers.GetOrCreateXEvmAddress(delegateAddress);

                delegation = new Eip7702Delegation
                {
                    Id = Cache.Chain.NextSubId(op),
                    ChainId = op.ChainId,
                    Level = op.Level,
                    Timestamp = op.Timestamp,
                    TransactionId = op.Id,
                    SenderId = (op as IParentOperation)!.SenderId,
                    AuthorityId = authority.Id,
                    Nonce = (int)nonce, // overflow is unlikely
                    PrevDelegateId = authority.Eip7702DelegateId,
                    DelegateId = @delegate?.Id,
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "EIP7702 authorization is invalid");
                continue;
            }
            #endregion

            #region apply
            Db.TryAttach(sender);
            sender.Eip7702DelegationCount++;
            sender.LastLevel = delegation.Level;
            sender.LastTimestamp = Context.Block.Timestamp;

            if (authority != sender)
            {
                Db.TryAttach(authority);
                authority.Eip7702DelegationCount++;
                authority.LastLevel = delegation.Level;
                authority.LastTimestamp = Context.Block.Timestamp;
            }
            authority.Counter = delegation.Nonce;
            authority.Eip7702DelegateId = delegation.DelegateId;

            if (@delegate != null && @delegate != sender && @delegate != authority)
            {
                Db.TryAttach(@delegate);
                @delegate.Eip7702DelegationCount++;
                @delegate.LastLevel = delegation.Level;
                @delegate.LastTimestamp = Context.Block.Timestamp;
            }

            if (op is XEvmTransactionOperation evmTx)
                evmTx.Eip7702DelegationCount = (evmTx.Eip7702DelegationCount ?? 0) + 1;
            else if (op is XEvmMichelsonTransactionOperation evmMichTx)
                evmMichTx.Eip7702DelegationCount = (evmMichTx.Eip7702DelegationCount ?? 0) + 1;
            else
                throw new InvalidOperationException("Invalid EIP7702 parent operation");

            Cache.Chain.Get().Eip7702DelegationCount++;
            #endregion

            Db.Eip7702Delegations.Add(delegation);
        }
    }

    public async Task Revert(ISourceOperation op)
    {
        var delegations = await Db.Eip7702Delegations
            .Where(x => x.TransactionId == op.Id)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        foreach (var delegation in delegations)
        {
            #region init
            var sender = (await Cache.Addresses.GetAsync(delegation.SenderId) as XEvmAddress)!;
            var authority = (await Cache.Addresses.GetAsync(delegation.AuthorityId) as XEvmUser)!;
            var @delegate = await Cache.Addresses.GetAsync(delegation.DelegateId) as XEvmAddress;
            #endregion

            #region revert
            Db.TryAttach(sender);
            sender.Eip7702DelegationCount--;
            sender.LastLevel = delegation.Level;
            sender.LastTimestamp = Context.Block.Timestamp;

            if (authority != sender)
            {
                Db.TryAttach(authority);
                authority.Eip7702DelegationCount--;
                authority.LastLevel = delegation.Level;
                authority.LastTimestamp = Context.Block.Timestamp;
                if (authority.IsEmpty()) await Helpers.RemoveXEvmUser(authority);
            }
            authority.Counter = delegation.Nonce - 1;
            authority.Eip7702DelegateId = delegation.PrevDelegateId;

            if (@delegate != null && @delegate != sender && @delegate != authority)
            {
                Db.TryAttach(@delegate);
                @delegate.Eip7702DelegationCount--;
                @delegate.LastLevel = delegation.Level;
                @delegate.LastTimestamp = Context.Block.Timestamp;
                if (@delegate.IsEmpty()) await Helpers.RemoveXEvmAddress(@delegate);
            }

            if (op is XEvmTransactionOperation evmTx)
                evmTx.Eip7702DelegationCount--;
            else if (op is XEvmMichelsonTransactionOperation evmMichTx)
                evmMichTx.Eip7702DelegationCount--;
            else
                throw new InvalidOperationException("Invalid EIP7702 parent operation");

            Cache.Chain.Get().Eip7702DelegationCount--;
            #endregion

            Db.Eip7702Delegations.Remove(delegation);
        }
    }
}
