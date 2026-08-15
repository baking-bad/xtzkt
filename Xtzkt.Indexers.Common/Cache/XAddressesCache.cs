using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class XAddressesCache(XtzktContext db, IChainCache chain, ChainConfig chainConfig)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<int, XAddress> CachedById = [];
    static Dictionary<string, XAddress> CachedByHash = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 120_000;
        TargetCap = size?.TargetCap ?? 100_000;
        CachedById = new(SoftCap + 4096);
        CachedByHash = new(SoftCap + 4096);
    }
    #endregion

    readonly XtzktContext Db = db;
    readonly IChainCache Chain = chain;
    readonly ChainConfig ChainConfig = chainConfig;

    public void Reset()
    {
        CachedById.Clear();
        CachedByHash.Clear();
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

    public void Add(XAddress address)
    {
        CachedById[address.Id] = address;
        CachedByHash[address.Hash] = address;
    }

    public void Remove(XAddress address)
    {
        CachedById.Remove(address.Id);
        CachedByHash.Remove(address.Hash);
    }

    public async Task Preload(IEnumerable<int> ids)
    {
        var missed = ids.Where(x => !CachedById.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            var addresses = await Db.Addresses
                .OfType<XAddress>()
                .Where(x => missed.Contains(x.Id))
                .ToListAsync();
            
            foreach (var address in addresses)
                Add(address);
        }
    }

    public async Task Preload(IEnumerable<string> hashes)
    {
        var missed = hashes.Where(x => !CachedByHash.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            var addresses = await Db.Addresses
                .OfType<XAddress>()
                .Where(x => x.ChainId == ChainConfig.Id && missed.Contains(x.Hash))
                .ToListAsync();

            foreach (var address in addresses)
                Add(address);
        }
    }

    public XAddress GetCached(int id)
    {
        return CachedById[id];
    }

    public XAddress GetCached(string hash)
    {
        return CachedByHash[hash];
    }

    public bool TryGetCached(string hash, [NotNullWhen(true)] out XAddress? address)
    {
        return CachedByHash.TryGetValue(hash, out address);
    }

    public async Task<XAddress?> GetOrDefaultAsync(string hash)
    {
        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses.FirstOrDefaultAsync(x => x.ChainId == ChainConfig.Id && x.Hash == hash) as XAddress;
            if (address == null)
                return null;

            Add(address);
        }
        return address;
    }

    public async Task<XAddress> GetAsync(int id)
    {
        if (!CachedById.TryGetValue(id, out var address))
        {
            address = await Db.Addresses.FirstOrDefaultAsync(x => x.Id == id) as XAddress
                ?? throw new Exception($"Address #{id} doesn't exist");

            Add(address);
        }

        return address;
    }

    public async Task<XAddress?> GetAsync(int? id)
    {
        if (id is not int _id) return null;

        if (!CachedById.TryGetValue(_id, out var address))
        {
            address = await Db.Addresses.FirstOrDefaultAsync(x => x.Id == _id) as XAddress
                ?? throw new Exception($"Address #{_id} doesn't exist");

            Add(address);
        }

        return address;
    }

    public async Task<XAddress> GetExistingAsync(string hash)
    {
        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses.FirstOrDefaultAsync(x => x.ChainId == ChainConfig.Id && x.Hash == hash) as XAddress
                ?? throw new Exception($"Address {hash} doesn't exist");

            Add(address);
        }
        return address;
    }
}
