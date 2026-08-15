using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class TicketBalancesCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<(int, long), TicketBalance> Cached = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 16_000;
        TargetCap = size?.TargetCap ?? 12_000;
        Cached = new(SoftCap + 1024);
    }
    #endregion

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

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

    public void Add(TicketBalance ticketBalance)
    {
        Cached[(ticketBalance.AddressId, ticketBalance.TicketId)] = ticketBalance;
    }

    public void Remove(TicketBalance ticketBalance)
    {
        Cached.Remove((ticketBalance.AddressId, ticketBalance.TicketId));
    }

    public TicketBalance Get(int addressId, long ticketId)
    {
        if (!Cached.TryGetValue((addressId, ticketId), out var ticketBalance))
            throw new Exception($"TicketBalance ({addressId}, {ticketId}) doesn't exist");
        return ticketBalance;
    }

    public bool TryGet(int addressId, long ticketId, [NotNullWhen(true)] out TicketBalance? ticketBalance)
    {
        return Cached.TryGetValue((addressId, ticketId), out ticketBalance);
    }

    public async Task Preload(IEnumerable<(int, long)> ids)
    {
        var missed = ids.Where(x => !Cached.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            for (int i = 0, n = 2048; i < missed.Count; i += n)
            {
                var corteges = string.Join(',', missed.Skip(i).Take(n).Select(x => $"({x.Item1}, '{x.Item2}'::bigint)"));
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
                var items = await Db.TicketBalances
                    .FromSqlRaw($"""
                        SELECT *
                        FROM "TicketBalances"
                        WHERE ("AddressId", "TicketId") IN ({corteges})
                        """)
                    .ToListAsync();
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

                foreach (var item in items)
                    Add(item);
            }
        }
    }
}
