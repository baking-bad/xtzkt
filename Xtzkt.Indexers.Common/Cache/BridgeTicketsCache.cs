using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class BridgeTicketsCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<long, BridgeTicket> CachedById = [];
    static Dictionary<HashKey, BridgeTicket> CachedByWeakHash = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 4000;
        TargetCap = size?.TargetCap ?? 3000;
        CachedById = new(SoftCap + 256);
        CachedByWeakHash = new(SoftCap + 256);
    }
    #endregion

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public void Reset()
    {
        CachedById.Clear();
        CachedByWeakHash.Clear();
    }

    public void Trim()
    {
        if (CachedById.Count > SoftCap)
        {
            var toRemove = CachedById.Values
                .OrderBy(x => x.LastLevel)
                .Take(CachedById.Count - TargetCap)
                .ToList();

            foreach (var item in toRemove)
                Remove(item);
        }
    }

    public void Add(BridgeTicket ticket)
    {
        CachedById[ticket.Id] = ticket;
        CachedByWeakHash[ticket.WeakHash] = ticket;
    }

    public void Remove(BridgeTicket ticket)
    {
        CachedById.Remove(ticket.Id);
        CachedByWeakHash.Remove(ticket.WeakHash);
    }

    public BridgeTicket GetCached(long id)
    {
        if (!CachedById.TryGetValue(id, out var ticket))
            throw new Exception($"BridgeTicket #{id} doesn't exist in the cache");
        return ticket;
    }

    public bool TryGetCached(byte[] hash, [NotNullWhen(true)] out BridgeTicket? ticket)
    {
        return CachedByWeakHash.TryGetValue(hash, out ticket);
    }

    public async Task Preload(IEnumerable<long> ids)
    {
        var missed = ids.Where(x => !CachedById.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            var items = await Db.BridgeTickets
                .Where(x => missed.Contains(x.Id))
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }

    public async Task Preload(IEnumerable<HashKey> hashes)
    {
        var missed = hashes
            .Where(x => !CachedByWeakHash.ContainsKey(x))
            .Distinct()
            .Select(x => x.Bytes)
            .ToList();

        if (missed.Count != 0)
        {
            var items = await Db.BridgeTickets
                .Where(x => x.ChainId == Chain.Id && missed.Contains(x.WeakHash))
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }
}
