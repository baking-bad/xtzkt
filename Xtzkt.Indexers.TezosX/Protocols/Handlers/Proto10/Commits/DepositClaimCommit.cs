using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10
{
    class DepositClaimCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply()
        {
            foreach (var (op, type, depositId) in await EnumerateClaims())
            {
                var deposit = Context.DepositOps
                    .OfType<XEvmDepositOperation>()
                    .FirstOrDefault(x => x.Type == type && x.DepositId == depositId)
                    ?? Db.ChangeTracker.Entries<XEvmDepositOperation>()
                    .Where(x => x.State == EntityState.Added)
                    .Select(x => x.Entity)
                    .FirstOrDefault(x => x.Type == type && x.DepositId == depositId)
                    ?? await FindDeposit(type, depositId);

                if (deposit == null)
                    continue;

                switch (op)
                {
                    case XEvmTransactionOperation transaction:
                        transaction.ClaimDepositId = deposit.Id;
                        break;
                    case XMichelsonEvmTransactionOperation transaction:
                        transaction.ClaimDepositId = deposit.Id;
                        break;
                }

                Db.TryAttach(deposit);
                deposit.ClaimTransactionId = op.Id;
            }
        }

        public virtual async Task Revert()
        {
            foreach (var (_, type, depositId) in await EnumerateClaims())
            {
                if (Context.DepositOps.OfType<XEvmDepositOperation>().Any(x => x.Type == type && x.DepositId == depositId))
                    continue;

                var deposit = await FindDeposit(type, depositId);
                if (deposit != null)
                {
                    Db.TryAttach(deposit);
                    deposit.ClaimTransactionId = null;
                }
            }
        }

        async Task<List<(TransactionOperation Op, DepositType Type, BigInteger DepositId)>> EnumerateClaims()
        {
            var claims = new List<(TransactionOperation, DepositType, BigInteger)>();

            foreach (var op in Context.TransactionOps)
            {
                if (op.Status != OperationStatus.Applied)
                    continue;

                var (input, targetId) = op switch
                {
                    XEvmTransactionOperation transaction => (transaction.Input, transaction.TargetId),
                    XMichelsonEvmTransactionOperation transaction => (transaction.Input, transaction.TargetId),
                    _ => (null, 0)
                };

                if (input == null || input.Length < 36) // selector + uint256 depositId
                    continue;

                DepositType type;
                if (input.AsSpan(0, 4).SequenceEqual(EvmRuntime.FaClaimSelector))
                    type = DepositType.Fa;
                else if (input.AsSpan(0, 4).SequenceEqual(EvmRuntime.XtzClaimSelector))
                    type = DepositType.Xtz;
                else
                    continue;

                var target = await Cache.Addresses.GetAsync(targetId);
                if (target.Hash != (type == DepositType.Fa ? EvmRuntime.FaBridge : EvmRuntime.XtzBridge))
                    continue;

                claims.Add((op, type, new BigInteger(input.AsSpan(4, 32), true, true)));
            }

            return claims;
        }

        Task<XEvmDepositOperation?> FindDeposit(DepositType type, BigInteger depositId)
        {
            return Db.DepositOps
                .OfType<XEvmDepositOperation>()
                .FirstOrDefaultAsync(x => x.ChainId == Context.Block.ChainId && x.Type == type && x.DepositId == depositId);
        }
    }
}
