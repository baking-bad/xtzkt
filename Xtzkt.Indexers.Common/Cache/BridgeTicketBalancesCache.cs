using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.Common.Cache;

public class BridgeTicketBalancesCache(XtzktContext db)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<(int, long), BridgeTicketBalance> Cached = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 16_000;
        TargetCap = size?.TargetCap ?? 12_000;
        Cached = new(SoftCap + 1024);
    }
    #endregion

    readonly XtzktContext Db = db;

    public void Reset()
    {
        Cached.Clear();
    }

    public void Trim()
    {
        if (Cached.Count > SoftCap)
        {
            var toRemove = Cached.Values
                .OrderBy(x => x.LastLevel)
                .Take(Cached.Count - TargetCap)
                .ToList();

            foreach (var item in toRemove)
                Remove(item);
        }
    }

    public void Add(BridgeTicketBalance balance)
    {
        Cached[(balance.AddressId, balance.TicketId)] = balance;
    }

    public void Remove(BridgeTicketBalance balance)
    {
        Cached.Remove((balance.AddressId, balance.TicketId));
    }

    public BridgeTicketBalance Get(int addressId, long ticketId)
    {
        if (!Cached.TryGetValue((addressId, ticketId), out var balance))
            throw new Exception($"BridgeTicketBalance ({addressId}, {ticketId}) doesn't exist");
        return balance;
    }

    public bool TryGet(int addressId, long ticketId, [NotNullWhen(true)] out BridgeTicketBalance? balance)
    {
        return Cached.TryGetValue((addressId, ticketId), out balance);
    }

    public async Task Preload(IEnumerable<(int, long)> ids)
    {
        var missed = ids.Where(x => !Cached.ContainsKey(x)).Distinct().ToList();
        if (missed.Count != 0)
        {
            var addressIds = missed.Select(x => x.Item1).ToArray();
            var ticketIds = missed.Select(x => x.Item2).ToArray();

            var items = await Db.BridgeTicketBalances
                .FromSqlRaw("""
                    SELECT b.*
                    FROM "BridgeTicketBalances" AS b
                    JOIN unnest({0}, {1}) AS k(address_id, ticket_id)
                    ON b."AddressId" = k.address_id AND b."TicketId" = k.ticket_id
                    """, addressIds, ticketIds)
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }
}
