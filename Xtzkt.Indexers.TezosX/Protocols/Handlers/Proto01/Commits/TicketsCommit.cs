using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01
{
    class TicketsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        readonly Dictionary<IOperation, Dictionary<TicketIdentity, List<(IOperation Op, TicketUpdate Update)>>> Updates = [];

        public virtual void Append(IOperation parent, IOperation op, IEnumerable<TicketUpdates> updates)
        {
            if (!Updates.TryGetValue(parent, out var opUpdates))
                Updates.Add(parent, opUpdates = []);

            foreach (var update in updates)
            {
                if (!opUpdates.TryGetValue(update.Ticket, out var ticketUpdates))
                    opUpdates.Add(update.Ticket, ticketUpdates = []);

                ticketUpdates.AddRange(update.Updates.Select(update => (op, update)));
            }
        }

        public virtual async Task Apply()
        {
            if (Updates.Count == 0) return;

            #region precache
            var addressesSet = new HashSet<string>();
            var ticketsSet = new HashSet<(int, TicketIdentity)>();
            var balancesSet = new HashSet<(int, long)>();

            foreach (var (ticket, updates) in Updates.SelectMany(x => x.Value))
            {
                addressesSet.Add(ticket.Ticketer);
                foreach (var (_, upd) in updates)
                    addressesSet.Add(upd.Address);
            }

            await Cache.Addresses.Preload(addressesSet);

            foreach (var (ticket, _) in Updates.SelectMany(x => x.Value))
            {
                if (Cache.Addresses.TryGetCached(ticket.Ticketer, out var ticketer))
                    ticketsSet.Add((ticketer.Id, ticket));
            }

            await Cache.Tickets.Preload(ticketsSet);

            foreach (var (ticket, updates) in Updates.SelectMany(x => x.Value))
            {
                if (Cache.Addresses.TryGetCached(ticket.Ticketer, out var ticketer))
                {
                    if (Cache.Tickets.TryGetCached(ticketer.Id, ticket.RawType, ticket.RawContent, out var _ticket))
                    {
                        foreach (var (_, upd) in updates)
                        {
                            if (Cache.Addresses.TryGetCached(upd.Address, out var acc))
                                balancesSet.Add((acc.Id, _ticket.Id));
                        }
                    }
                }
            }

            await Cache.TicketBalances.Preload(balancesSet);
            #endregion

            Context.Block.Events |= XBlockEvents.Tickets;

            foreach (var (_, opUpdates) in Updates.OrderBy(kv => kv.Key.Id))
            {
                foreach (var (ticketIdentity, ticketUpdates) in opUpdates
                    .OrderBy(x => x.Value[0].Op.Id)
                    .ThenBy(x => x.Key.WeakHash, BytesComparer.Instance)
                    .ThenBy(x => x.Key.RawType, BytesComparer.Instance))
                {
                    var ticketer = (Cache.Addresses.GetCached(ticketIdentity.Ticketer) as XMichelsonContract)!;
                    var ticket = GetOrCreateTicket(ticketUpdates[0].Op, ticketer, ticketIdentity);

                    if (ticketUpdates.Count == 1 || ticketUpdates.BigSum(x => x.Update.Amount) != BigInteger.Zero)
                    {
                        foreach (var (op, ticketUpdate) in ticketUpdates)
                            await MintOrBurnTickets(op, ticket, ticketUpdate.Address, ticketUpdate.Amount);
                    }
                    else if (ticketUpdates.Count(x => x.Update.Amount < BigInteger.Zero) == 1)
                    {
                        var (fromOp, fromUpdate) = ticketUpdates.First(x => x.Update.Amount < BigInteger.Zero);
                        foreach (var (op, ticketUpdate) in ticketUpdates.Where(x => x.Update.Amount > BigInteger.Zero))
                            await TransferTickets(ticketUpdates[0].Op, ticket, fromUpdate.Address, ticketUpdate.Address, ticketUpdate.Amount);
                    }
                    else if (ticketUpdates.Count(x => x.Update.Amount > BigInteger.Zero) == 1)
                    {
                        var (toOp, toUpdate) = ticketUpdates.First(x => x.Update.Amount > BigInteger.Zero);
                        foreach (var (op, ticketUpdate) in ticketUpdates.Where(x => x.Update.Amount < BigInteger.Zero))
                            await TransferTickets(ticketUpdates[0].Op, ticket, ticketUpdate.Address, toUpdate.Address, -ticketUpdate.Amount);
                    }
                    else if (IsTransfersSequence(ticketUpdates))
                    {
                        for (int i = 0; i < ticketUpdates.Count; i += 2)
                        {
                            var u1 = ticketUpdates[i].Update;
                            var u2 = ticketUpdates[i + 1].Update;

                            if (u1.Amount < 0) // from u1 to u2
                                await TransferTickets(ticketUpdates[i].Op, ticket, u1.Address, u2.Address, u2.Amount);
                            else // from u2 to u1
                                await TransferTickets(ticketUpdates[i].Op, ticket, u2.Address, u1.Address, u1.Amount);
                        }
                    }
                    else
                    {
                        foreach (var (op, ticketUpdate) in ticketUpdates)
                            await MintOrBurnTickets(op, ticket, ticketUpdate.Address, ticketUpdate.Amount);
                    }
                }
            }
        }

        static bool IsTransfersSequence(List<(IOperation Op, TicketUpdate Update)> updates)
        {
            if (updates.Count % 2 != 0)
                return false;

            for (int i = 0; i < updates.Count; i += 2)
                if (updates[i].Update.Amount > 0 || updates[i].Update.Amount != -updates[i + 1].Update.Amount)
                    return false;

            return true;
        }

        Ticket GetOrCreateTicket(IOperation op, XMichelsonContract ticketer, TicketIdentity ticketToken)
        {
            if (!Cache.Tickets.TryGetCached(ticketer.Id, ticketToken.RawType, ticketToken.RawContent, out var ticket))
            {
                ticket = new Ticket
                {
                    Id = op switch
                    {
                        XMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                        XEvmMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                        TransferTicketOperation transferTicket => Cache.Chain.NextSubId(transferTicket),
                        SmartRollupExecuteOperation srExecute => Cache.Chain.NextSubId(srExecute),
                        _ => throw new ArgumentOutOfRangeException(nameof(op))
                    },
                    ChainId = op.ChainId,
                    TicketerId = ticketer.Id,
                    FirstMinterId = op switch
                    {
                        XMichelsonTransactionOperation transaction => transaction.InitiatorId ?? transaction.SenderId,
                        XEvmMichelsonTransactionOperation transaction => transaction.InitiatorId ?? transaction.SenderId,
                        TransferTicketOperation transferTicket => transferTicket.SenderId,
                        SmartRollupExecuteOperation srExecute => srExecute.SenderId,
                        _ => throw new ArgumentOutOfRangeException(nameof(op))
                    },
                    FirstLevel = op.Level,
                    FirstTimestamp = op.Timestamp,
                    LastLevel = op.Level,
                    LastTimestamp = op.Timestamp,
                    TotalBurned = BigInteger.Zero,
                    TotalMinted = BigInteger.Zero,
                    TotalSupply = BigInteger.Zero,
                    WeakHash = ticketToken.WeakHash,
                    RawType = ticketToken.RawType,
                    RawContent = ticketToken.RawContent,
                    JsonContent = ticketToken.JsonContent,
                };

                Db.Tickets.Add(ticket);
                Cache.Tickets.Add(ticket);

                Db.TryAttach(ticketer);
                ticketer.TicketsCount++;
                ticketer.LastLevel = op.Level;
                ticketer.LastTimestamp = op.Timestamp;

                var state = Cache.Chain.Get();
                state.TicketsCount++;
            }
            return ticket;
        }

        TicketBalance GetOrCreateTicketBalance(IOperation op, Ticket ticket, XMichelsonAddress address)
        {
            if (!Cache.TicketBalances.TryGet(address.Id, ticket.Id, out var ticketBalance))
            {
                ticketBalance = new TicketBalance
                {
                    Id = op switch
                    {
                        XMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                        XEvmMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                        TransferTicketOperation transferTicket => Cache.Chain.NextSubId(transferTicket),
                        SmartRollupExecuteOperation srExecute => Cache.Chain.NextSubId(srExecute),
                        _ => throw new ArgumentOutOfRangeException(nameof(op))
                    },
                    ChainId = address.ChainId,
                    AddressId = address.Id,
                    TicketId = ticket.Id,
                    TicketerId = ticket.TicketerId,
                    FirstLevel = op.Level,
                    FirstTimestamp = op.Timestamp,
                    LastLevel = op.Level,
                    LastTimestamp = op.Timestamp,
                    Balance = BigInteger.Zero
                };

                Db.TicketBalances.Add(ticketBalance);
                Cache.TicketBalances.Add(ticketBalance);

                Db.TryAttach(ticket);
                ticket.BalancesCount++;

                Db.TryAttach(address);
                address.TicketBalancesCount++;
                address.LastLevel = op.Level;
                address.LastTimestamp = op.Timestamp;

                var state = Cache.Chain.Get();
                state.TicketBalancesCount++;
            }
            return ticketBalance;
        }

        async Task TransferTickets(IOperation op, Ticket ticket, string fromAddress, string toAddress, BigInteger amount)
        {
            var from = await GetCachedOrCreateXMichelsonAddress(fromAddress);
            var fromBalance = GetOrCreateTicketBalance(op, ticket, from);
            var to = await GetCachedOrCreateXMichelsonAddress(toAddress);
            var toBalance = GetOrCreateTicketBalance(op, ticket, to);

            switch (op)
            {
                case XMichelsonTransactionOperation transaction:
                    transaction.TicketTransfers = (transaction.TicketTransfers ?? 0) + 1;
                    break;
                case XEvmMichelsonTransactionOperation transaction:
                    transaction.TicketTransfers = (transaction.TicketTransfers ?? 0) + 1;
                    break;
                case TransferTicketOperation transferTicket:
                    transferTicket.TicketTransfers = (transferTicket.TicketTransfers ?? 0) + 1;
                    break;
                case SmartRollupExecuteOperation srExecute:
                    srExecute.TicketTransfers = (srExecute.TicketTransfers ?? 0) + 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }

            Db.TryAttach(from);
            from.TicketTransfersCount++;
            from.LastLevel = op.Level;
            from.LastTimestamp = op.Timestamp;

            Db.TryAttach(to);
            if (to != from)
            {
                to.TicketTransfersCount++;
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

            Db.TryAttach(ticket);
            ticket.TransfersCount++;
            ticket.LastLevel = op.Level;
            ticket.LastTimestamp = op.Timestamp;
            if (amount != BigInteger.Zero && fromBalance != toBalance)
            {
                if (fromBalance.Balance == BigInteger.Zero)
                {
                    from.ActiveTicketsCount--;
                    ticket.HoldersCount--;
                }
                if (toBalance.Balance == amount)
                {
                    to.ActiveTicketsCount++;
                    ticket.HoldersCount++;
                }
            }

            var state = Cache.Chain.Get();
            state.TicketTransfersCount++;

            Db.TicketTransfers.Add(new TicketTransfer
            {
                Id = op switch
                {
                    XMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                    XEvmMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                    TransferTicketOperation transferTicket => Cache.Chain.NextSubId(transferTicket),
                    SmartRollupExecuteOperation srExecute => Cache.Chain.NextSubId(srExecute),
                    _ => throw new ArgumentOutOfRangeException(nameof(op))
                },
                ChainId = op.ChainId,
                Amount = amount,
                FromId = from.Id,
                ToId = to.Id,
                Level = op.Level,
                Timestamp = op.Timestamp,
                TicketId = ticket.Id,
                TicketerId = ticket.TicketerId,
                TransactionId = (op as TransactionOperation)?.Id,
                TransferTicketId = (op as TransferTicketOperation)?.Id,
                SmartRollupExecuteId = (op as SmartRollupExecuteOperation)?.Id
            });
        }

        async Task MintOrBurnTickets(IOperation op, Ticket ticket, string addressHash, BigInteger amount)
        {
            var address = await GetCachedOrCreateXMichelsonAddress(addressHash);
            var balance = GetOrCreateTicketBalance(op, ticket, address);

            switch (op)
            {
                case XMichelsonTransactionOperation transaction:
                    transaction.TicketTransfers = (transaction.TicketTransfers ?? 0) + 1;
                    break;
                case XEvmMichelsonTransactionOperation transaction:
                    transaction.TicketTransfers = (transaction.TicketTransfers ?? 0) + 1;
                    break;
                case TransferTicketOperation transferTicket:
                    transferTicket.TicketTransfers = (transferTicket.TicketTransfers ?? 0) + 1;
                    break;
                case SmartRollupExecuteOperation srExecute:
                    srExecute.TicketTransfers = (srExecute.TicketTransfers ?? 0) + 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }

            Db.TryAttach(address);
            address.TicketTransfersCount++;
            address.LastLevel = op.Level;
            address.LastTimestamp = op.Timestamp;

            Db.TryAttach(balance);
            balance.Balance += amount;
            balance.TransfersCount++;
            balance.LastLevel = op.Level;
            balance.LastTimestamp = op.Timestamp;

            Db.TryAttach(ticket);
            ticket.TransfersCount++;
            ticket.LastLevel = op.Level;
            ticket.LastTimestamp = op.Timestamp;
            ticket.TotalSupply += amount;
            if (amount > BigInteger.Zero)
            {
                ticket.TotalMinted += amount;
                if (balance.Balance == amount)
                {
                    address.ActiveTicketsCount++;
                    ticket.HoldersCount++;
                }
            }
            else if (amount < BigInteger.Zero)
            {
                ticket.TotalBurned += -amount;
                if (balance.Balance == BigInteger.Zero)
                {
                    address.ActiveTicketsCount--;
                    ticket.HoldersCount--;
                }
            }

            var state = Cache.Chain.Get();
            state.TicketTransfersCount++;

            Db.TicketTransfers.Add(new TicketTransfer
            {
                Id = op switch
                {
                    XMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                    XEvmMichelsonTransactionOperation transaction => Cache.Chain.NextSubId(transaction),
                    TransferTicketOperation transferTicket => Cache.Chain.NextSubId(transferTicket),
                    SmartRollupExecuteOperation srExecute => Cache.Chain.NextSubId(srExecute),
                    _ => throw new ArgumentOutOfRangeException(nameof(op))
                },
                ChainId = op.ChainId,
                Amount = amount > BigInteger.Zero ? amount : -amount,
                FromId = amount < BigInteger.Zero ? address.Id : null,
                ToId = amount > BigInteger.Zero ? address.Id : null,
                Level = op.Level,
                Timestamp = op.Timestamp,
                TicketId = ticket.Id,
                TicketerId = ticket.TicketerId,
                TransactionId = (op as TransactionOperation)?.Id,
                TransferTicketId = (op as TransferTicketOperation)?.Id,
                SmartRollupExecuteId = (op as SmartRollupExecuteOperation)?.Id
            });
        }

        public virtual async Task Revert(XBlock block)
        {
            if (!block.Events.HasFlag(XBlockEvents.Tickets))
                return;

            var state = Cache.Chain.Get();

            var transfers = await Db.TicketTransfers
                .AsNoTracking()
                .Where(x => x.ChainId == block.ChainId && x.Level == block.Level)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            #region precache
            var addressesSet = new HashSet<int>();
            var ticketsSet = new HashSet<long>();
            var balancesSet = new HashSet<(int, long)>();

            foreach (var tr in transfers)
                ticketsSet.Add(tr.TicketId);

            await Cache.Tickets.Preload(ticketsSet);

            foreach (var tr in transfers)
            {
                if (tr.FromId is int fromId)
                {
                    addressesSet.Add(fromId);
                    balancesSet.Add((fromId, tr.TicketId));
                }

                if (tr.ToId is int toId)
                {
                    addressesSet.Add(toId);
                    balancesSet.Add((toId, tr.TicketId));
                }
            }

            foreach (var id in ticketsSet)
            {
                var ticket = Cache.Tickets.GetCached(id);
                addressesSet.Add(ticket.TicketerId);
            }

            await Cache.Addresses.Preload(addressesSet);
            await Cache.TicketBalances.Preload(balancesSet);
            #endregion

            var ticketsToRemove = new HashSet<Ticket>();
            var ticketBalancesToRemove = new HashSet<TicketBalance>();
            var addressesToRemove = new HashSet<XMichelsonAddress>();

            foreach (var transfer in transfers)
            {
                var ticket = Cache.Tickets.GetCached(transfer.TicketId);
                Db.TryAttach(ticket);
                ticket.TransfersCount--;
                ticket.LastLevel = block.Level;
                ticket.LastTimestamp = block.Timestamp;
                if (ticket.TransfersCount == 0)
                    ticketsToRemove.Add(ticket);

                state.TicketTransfersCount--;

                if (transfer.FromId is int fromId && transfer.ToId is int toId)
                {
                    #region revert transfer
                    var from = (Cache.Addresses.GetCached(fromId) as XMichelsonAddress)!;
                    var fromBalance = Cache.TicketBalances.Get(from.Id, ticket.Id);
                    var to = (Cache.Addresses.GetCached(toId) as XMichelsonAddress)!;
                    var toBalance = Cache.TicketBalances.Get(to.Id, ticket.Id);

                    Db.TryAttach(from);
                    from.TicketTransfersCount--;
                    from.LastLevel = block.Level;
                    from.LastTimestamp = block.Timestamp;
                    if (from.IsEmpty()) addressesToRemove.Add(from);

                    Db.TryAttach(to);
                    if (to != from)
                    {
                        to.TicketTransfersCount--;
                        to.LastLevel = block.Level;
                        to.LastTimestamp = block.Timestamp;
                        if (to.IsEmpty()) addressesToRemove.Add(to);
                    }

                    Db.TryAttach(fromBalance);
                    fromBalance.Balance += transfer.Amount;
                    fromBalance.TransfersCount--;
                    fromBalance.LastLevel = block.Level;
                    fromBalance.LastTimestamp = block.Timestamp;
                    if (fromBalance.TransfersCount == 0)
                        ticketBalancesToRemove.Add(fromBalance);

                    Db.TryAttach(toBalance);
                    toBalance.Balance -= transfer.Amount;
                    if (toBalance != fromBalance) toBalance.TransfersCount--;
                    toBalance.LastLevel = block.Level;
                    toBalance.LastTimestamp = block.Timestamp;
                    if (toBalance.TransfersCount == 0)
                        ticketBalancesToRemove.Add(toBalance);

                    if (transfer.Amount != BigInteger.Zero && fromBalance != toBalance)
                    {
                        if (fromBalance.Balance == transfer.Amount)
                        {
                            from.ActiveTicketsCount++;
                            ticket.HoldersCount++;
                        }
                        if (toBalance.Balance == BigInteger.Zero)
                        {
                            to.ActiveTicketsCount--;
                            ticket.HoldersCount--;
                        }
                    }
                    #endregion
                }
                else if (transfer.ToId != null)
                {
                    #region revert mint
                    var to = (Cache.Addresses.GetCached(transfer.ToId.Value) as XMichelsonAddress)!;
                    var toBalance = Cache.TicketBalances.Get(to.Id, ticket.Id);

                    Db.TryAttach(to);
                    to.TicketTransfersCount--;
                    to.LastLevel = block.Level;
                    to.LastTimestamp = block.Timestamp;
                    if (to.IsEmpty()) addressesToRemove.Add(to);

                    Db.TryAttach(toBalance);
                    toBalance.Balance -= transfer.Amount;
                    toBalance.TransfersCount--;
                    toBalance.LastLevel = block.Level;
                    toBalance.LastTimestamp = block.Timestamp;
                    if (toBalance.TransfersCount == 0)
                        ticketBalancesToRemove.Add(toBalance);

                    if (transfer.Amount != BigInteger.Zero)
                    {
                        ticket.TotalSupply -= transfer.Amount;
                        ticket.TotalMinted -= transfer.Amount;
                        if (toBalance.Balance == BigInteger.Zero)
                        {
                            to.ActiveTicketsCount--;
                            ticket.HoldersCount--;
                        }
                    }
                    #endregion
                }
                else
                {
                    #region revert burn
                    var from = (Cache.Addresses.GetCached(transfer.FromId!.Value) as XMichelsonAddress)!;
                    var fromBalance = Cache.TicketBalances.Get(from.Id, ticket.Id);

                    Db.TryAttach(from);
                    from.TicketTransfersCount--;
                    from.LastLevel = block.Level;
                    from.LastTimestamp = block.Timestamp;
                    if (from.IsEmpty()) addressesToRemove.Add(from);

                    Db.TryAttach(fromBalance);
                    fromBalance.Balance += transfer.Amount;
                    fromBalance.TransfersCount--;
                    fromBalance.LastLevel = block.Level;
                    fromBalance.LastTimestamp = block.Timestamp;
                    if (fromBalance.TransfersCount == 0)
                        ticketBalancesToRemove.Add(fromBalance);

                    if (transfer.Amount != BigInteger.Zero)
                    {
                        ticket.TotalSupply += transfer.Amount;
                        ticket.TotalBurned -= transfer.Amount;
                        if (fromBalance.Balance == transfer.Amount)
                        {
                            from.ActiveTicketsCount++;
                            ticket.HoldersCount++;
                        }
                    }
                    #endregion
                }
            }

            foreach (var ticketBalance in ticketBalancesToRemove)
            {
                Db.TicketBalances.Remove(ticketBalance);
                Cache.TicketBalances.Remove(ticketBalance);

                var t = Cache.Tickets.GetCached(ticketBalance.TicketId);
                Db.TryAttach(t);
                t.BalancesCount--;

                var a = Cache.Addresses.GetCached(ticketBalance.AddressId);
                Db.TryAttach(a);
                a.TicketBalancesCount--;
                a.LastLevel = block.Level;
                a.LastTimestamp = block.Timestamp;

                state.TicketBalancesCount--;
            }

            foreach (var ticket in ticketsToRemove)
            {
                Db.Tickets.Remove(ticket);
                Cache.Tickets.Remove(ticket);

                var contract = (XMichelsonContract)Cache.Addresses.GetCached(ticket.TicketerId);
                Db.TryAttach(contract);
                contract.TicketsCount--;
                contract.LastLevel = block.Level;
                contract.LastTimestamp = block.Timestamp;

                state.TicketsCount--;
            }

            foreach (var address in addressesToRemove)
                await RemoveXMichelsonAddress(address);

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "TicketTransfers"
                WHERE "ChainId" = {0}
                AND "Level" = {1}
                """, block.ChainId, block.Level);
        }
    }
}
