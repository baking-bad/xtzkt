using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01
{
    class BridgeTicketsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply()
        {
            if (Context.BridgeTicketUpdates.Count == 0)
                return;

            #region precache
            var hashesSet = new HashSet<HashKey>();
            var addressesSet = new HashSet<string>();

            foreach (var update in Context.BridgeTicketUpdates)
            {
                hashesSet.Add(update.TicketHash);
                addressesSet.Add(update.To ?? update.From!);
            }

            await Cache.BridgeTickets.Preload(hashesSet);
            await Cache.Addresses.Preload(addressesSet);

            var balancesSet = new HashSet<(int, long)>();
            foreach (var update in Context.BridgeTicketUpdates)
            {
                if (Cache.BridgeTickets.TryGetCached(update.TicketHash, out var ticket) &&
                    Cache.Addresses.TryGetCached(update.To ?? update.From!, out var address))
                    balancesSet.Add((address.Id, ticket.Id));
            }

            await Cache.BridgeTicketBalances.Preload(balancesSet);
            #endregion

            Context.Block.Events |= XBlockEvents.BridgeTickets;

            var state = Cache.Chain.Get();

            foreach (var update in Context.BridgeTicketUpdates.OrderBy(x => x.Op.Id))
            {
                var op = update.Op;
                var ticket = GetOrCreateBridgeTicket(op, update.TicketHash);
                var address = await Helpers.GetOrCreateXEvmAddress(update.To ?? update.From!);
                var balance = GetOrCreateBridgeTicketBalance(op, ticket, address);
                var diff = update.To != null ? update.Amount : -update.Amount;

                switch (op)
                {
                    case XEvmTransactionOperation transaction:
                        transaction.BridgeTicketTransfers = (transaction.BridgeTicketTransfers ?? 0) + 1;
                        break;
                    case XMichelsonEvmTransactionOperation transaction:
                        transaction.BridgeTicketTransfers = (transaction.BridgeTicketTransfers ?? 0) + 1;
                        break;
                    case XEvmDepositOperation deposit:
                        deposit.BridgeTicketTransfers = (deposit.BridgeTicketTransfers ?? 0) + 1;
                        break;
                    default:
                        // the bridge is a predeployed contract, so its events can only be emitted
                        // in a CALL frame — never in a CREATE one, and never in a michelson runtime
                        throw new ArgumentOutOfRangeException(nameof(op));
                }

                Db.TryAttach(address);
                address.BridgeTicketTransfersCount++;
                address.LastLevel = op.Level;
                address.LastTimestamp = op.Timestamp;

                Db.TryAttach(balance);
                balance.Balance += diff;
                balance.TransfersCount++;
                balance.LastLevel = op.Level;
                balance.LastTimestamp = op.Timestamp;

                Db.TryAttach(ticket);
                ticket.TransfersCount++;
                ticket.LastLevel = op.Level;
                ticket.LastTimestamp = op.Timestamp;
                ticket.TotalSupply += diff;
                if (diff > BigInteger.Zero)
                {
                    ticket.TotalMinted += diff;
                    if (balance.Balance == diff)
                    {
                        address.ActiveBridgeTicketsCount++;
                        ticket.HoldersCount++;
                    }
                }
                else if (diff < BigInteger.Zero)
                {
                    ticket.TotalBurned += -diff;
                    if (balance.Balance == BigInteger.Zero)
                    {
                        address.ActiveBridgeTicketsCount--;
                        ticket.HoldersCount--;
                    }
                }

                state.BridgeTicketTransfersCount++;

                Db.BridgeTicketTransfers.Add(new BridgeTicketTransfer
                {
                    Id = Cache.Chain.NextSubId(op),
                    ChainId = op.ChainId,
                    TicketId = ticket.Id,
                    Level = op.Level,
                    Timestamp = op.Timestamp,
                    Amount = update.Amount,
                    FromId = update.From != null ? address.Id : null,
                    ToId = update.To != null ? address.Id : null,
                    TransactionId = (op as TransactionOperation)?.Id,
                    DepositId = (op as DepositOperation)?.Id
                });
            }
        }

        BridgeTicket GetOrCreateBridgeTicket(ISourceOperation op, byte[] weakHash)
        {
            if (!Cache.BridgeTickets.TryGetCached(weakHash, out var ticket))
            {
                ticket = new BridgeTicket
                {
                    Id = Cache.Chain.NextSubId(op),
                    ChainId = op.ChainId,
                    WeakHash = weakHash,
                    FirstLevel = op.Level,
                    FirstTimestamp = op.Timestamp,
                    LastLevel = op.Level,
                    LastTimestamp = op.Timestamp,
                    TotalMinted = BigInteger.Zero,
                    TotalBurned = BigInteger.Zero,
                    TotalSupply = BigInteger.Zero
                };

                Db.BridgeTickets.Add(ticket);
                Cache.BridgeTickets.Add(ticket);

                Cache.Chain.Get().BridgeTicketsCount++;
            }
            return ticket;
        }

        BridgeTicketBalance GetOrCreateBridgeTicketBalance(ISourceOperation op, BridgeTicket ticket, XEvmAddress address)
        {
            if (!Cache.BridgeTicketBalances.TryGet(address.Id, ticket.Id, out var balance))
            {
                balance = new BridgeTicketBalance
                {
                    Id = Cache.Chain.NextSubId(op),
                    ChainId = op.ChainId,
                    TicketId = ticket.Id,
                    AddressId = address.Id,
                    FirstLevel = op.Level,
                    FirstTimestamp = op.Timestamp,
                    LastLevel = op.Level,
                    LastTimestamp = op.Timestamp,
                    Balance = BigInteger.Zero
                };

                Db.BridgeTicketBalances.Add(balance);
                Cache.BridgeTicketBalances.Add(balance);

                Db.TryAttach(ticket);
                ticket.BalancesCount++;

                Db.TryAttach(address);
                address.BridgeTicketBalancesCount++;
                address.LastLevel = op.Level;
                address.LastTimestamp = op.Timestamp;

                Cache.Chain.Get().BridgeTicketBalancesCount++;
            }
            return balance;
        }

        public virtual async Task Revert(XBlock block)
        {
            if (!block.Events.HasFlag(XBlockEvents.BridgeTickets))
                return;

            var state = Cache.Chain.Get();

            var transfers = await Db.BridgeTicketTransfers
                .AsNoTracking()
                .Where(x => x.ChainId == block.ChainId && x.Level == block.Level)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            #region precache
            var addressesSet = new HashSet<int>();
            var ticketsSet = new HashSet<long>();
            var balancesSet = new HashSet<(int, long)>();

            foreach (var tr in transfers)
            {
                ticketsSet.Add(tr.TicketId);

                if ((tr.ToId ?? tr.FromId) is int addressId)
                {
                    addressesSet.Add(addressId);
                    balancesSet.Add((addressId, tr.TicketId));
                }
            }

            await Cache.BridgeTickets.Preload(ticketsSet);
            await Cache.Addresses.Preload(addressesSet);
            await Cache.BridgeTicketBalances.Preload(balancesSet);
            #endregion

            var ticketsToRemove = new HashSet<BridgeTicket>();
            var balancesToRemove = new HashSet<BridgeTicketBalance>();
            var addressesToRemove = new HashSet<XEvmAddress>();

            foreach (var transfer in transfers)
            {
                var ticket = Cache.BridgeTickets.GetCached(transfer.TicketId);
                Db.TryAttach(ticket);
                ticket.TransfersCount--;
                ticket.LastLevel = block.Level;
                ticket.LastTimestamp = block.Timestamp;
                if (ticket.TransfersCount == 0)
                    ticketsToRemove.Add(ticket);

                var address = (Cache.Addresses.GetCached((transfer.ToId ?? transfer.FromId)!.Value) as XEvmAddress)!;
                var balance = Cache.BridgeTicketBalances.Get(address.Id, ticket.Id);

                Db.TryAttach(address);
                address.BridgeTicketTransfersCount--;
                address.LastLevel = block.Level;
                address.LastTimestamp = block.Timestamp;
                if (address.IsEmpty()) addressesToRemove.Add(address);

                Db.TryAttach(balance);
                balance.TransfersCount--;
                balance.LastLevel = block.Level;
                balance.LastTimestamp = block.Timestamp;
                if (balance.TransfersCount == 0)
                    balancesToRemove.Add(balance);

                if (transfer.ToId != null)
                {
                    #region revert mint
                    balance.Balance -= transfer.Amount;
                    if (transfer.Amount != BigInteger.Zero)
                    {
                        ticket.TotalMinted -= transfer.Amount;
                        ticket.TotalSupply -= transfer.Amount;
                        if (balance.Balance == BigInteger.Zero)
                        {
                            address.ActiveBridgeTicketsCount--;
                            ticket.HoldersCount--;
                        }
                    }
                    #endregion
                }
                else
                {
                    #region revert burn
                    balance.Balance += transfer.Amount;
                    if (transfer.Amount != BigInteger.Zero)
                    {
                        ticket.TotalBurned -= transfer.Amount;
                        ticket.TotalSupply += transfer.Amount;
                        if (balance.Balance == transfer.Amount)
                        {
                            address.ActiveBridgeTicketsCount++;
                            ticket.HoldersCount++;
                        }
                    }
                    #endregion
                }

                state.BridgeTicketTransfersCount--;
            }

            foreach (var balance in balancesToRemove)
            {
                Db.BridgeTicketBalances.Remove(balance);
                Cache.BridgeTicketBalances.Remove(balance);

                var ticket = Cache.BridgeTickets.GetCached(balance.TicketId);
                Db.TryAttach(ticket);
                ticket.BalancesCount--;

                var address = (Cache.Addresses.GetCached(balance.AddressId) as XEvmAddress)!;
                Db.TryAttach(address);
                address.BridgeTicketBalancesCount--;
                address.LastLevel = block.Level;
                address.LastTimestamp = block.Timestamp;

                state.BridgeTicketBalancesCount--;
            }

            foreach (var ticket in ticketsToRemove)
            {
                Db.BridgeTickets.Remove(ticket);
                Cache.BridgeTickets.Remove(ticket);

                state.BridgeTicketsCount--;
            }

            foreach (var address in addressesToRemove)
                await Helpers.RemoveXEvmAddress(address);

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BridgeTicketTransfers"
                WHERE "ChainId" = {0}
                AND "Level" = {1}
                """, block.ChainId, block.Level);
        }
    }
}
