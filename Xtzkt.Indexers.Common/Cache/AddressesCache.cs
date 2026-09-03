using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class AddressesCache(XtzktContext db, IChainCache chain, ChainConfig chainConfig)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<int, L1Address> CachedById = [];
    static Dictionary<string, L1Address> CachedByHash = [];

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

    public async Task ResetAsync()
    {
        CachedById.Clear();
        CachedByHash.Clear();

        var bakers = await Db.Addresses
            .AsNoTracking()
            .OfType<L1Baker>()
            .Where(x => x.ChainId == ChainConfig.Id && x.Type == AddressType.L1Baker)
            .ToListAsync();

        foreach (var baker in bakers)
            Add(baker);
    }

    public void Trim()
    {
        if (CachedById.Count > SoftCap)
        {
            var toRemove = CachedById.Values
                .Where(x => x.Type != AddressType.L1Baker)
                .OrderBy(x => x.LastLevel)
                .Take(CachedById.Count - TargetCap)
                .ToList();

            foreach (var item in toRemove)
                Remove(item);
        }
    }

    public void Add(L1Address address)
    {
        CachedById[address.Id] = address;
        CachedByHash[address.Hash] = address;
    }

    public void Update(L1Address address)
    {
        if (CachedById.ContainsKey(address.Id))
        {
            CachedById[address.Id] = address;
            CachedByHash[address.Hash] = address;
        }
    }

    public void Remove(L1Address address)
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
                .OfType<L1Address>()
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
                .OfType<L1Address>()
                .Where(x => x.ChainId == ChainConfig.Id && missed.Contains(x.Hash))
                .ToListAsync();

            foreach (var address in addresses)
                Add(address);
        }
    }

    public async Task LoadAsync(IEnumerable<string> hashes, int level, DateTime timestamp)
    {
        var missed = hashes.Where(x => !CachedByHash.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            var addresses = await Db.Addresses
                .OfType<L1Address>()
                .Where(x => x.ChainId == ChainConfig.Id && missed.Contains(x.Hash))
                .ToListAsync();

            foreach (var address in addresses)
                Add(address);

            if (addresses.Count != missed.Count)
            {
                foreach (var hash in missed.Where(x => !CachedByHash.ContainsKey(x) && x[0] == 't' && x[1] == 'z'))
                {
                    var user = CreateUser(hash, level, timestamp);
                    Add(user);
                }
            }
        }
    }

    public async Task<bool> ExistsAsync(string hash, AddressType? type = null)
    {
        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses.FirstOrDefaultAsync(x => x.ChainId == ChainConfig.Id && x.Hash == hash) as L1Address;
            if (address != null) Add(address);
        }

        return address != null && (type == null || address.Type == type);
    }

    public L1Address GetCached(int id)
    {
        return CachedById[id];
    }

    public bool TryGetCached(string hash, [NotNullWhen(true)] out L1Address? address)
    {
        return CachedByHash.TryGetValue(hash, out address);
    }

    public async Task<L1Address> GetOrCreateAsync(string hash, L1Block block)
    {
        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses
                .FromSqlRaw("""
                    SELECT *
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "Hash" = {1}
                    """, ChainConfig.Id, hash)
                .SingleOrDefaultAsync() as L1Address;

            if (address == null)
            {
                address = hash[0] == 't' && hash[1] == 'z'
                    ? new L1User
                    {
                        Id = Chain.NextAddressId(),
                        ChainId = ChainConfig.Id,
                        Hash = hash,
                        FirstLevel = block.Level,
                        FirstTimestamp = block.Timestamp,
                        LastLevel = block.Level,
                        LastTimestamp = block.Timestamp,
                    }
                    : new L1Ghost
                    {
                        Id = Chain.NextAddressId(),
                        ChainId = ChainConfig.Id,
                        Hash = hash,
                        FirstLevel = block.Level,
                        FirstTimestamp = block.Timestamp,
                        LastLevel = block.Level,
                        LastTimestamp = block.Timestamp,
                    };

                Db.Addresses.Add(address);
            }

            Add(address);
        }

        if (address.Balance == 0 && address.Type == AddressType.L1User)
        {
            Db.TryAttach(address);
            address.Counter = Chain.GetManagerCounter();
        }

        return address;
    }

    public async Task<L1Address> GetAsync(int id)
    {
        if (!CachedById.TryGetValue(id, out var address))
        {
            address = await Db.Addresses.FirstOrDefaultAsync(x => x.Id == id) as L1Address
                ?? throw new Exception($"Address #{id} doesn't exist");

            Add(address);
        }

        return address;
    }

    public async Task<L1Address?> GetAsync(int? id)
    {
        if (id is not int _id) return null;

        if (!CachedById.TryGetValue(_id, out var address))
        {
            address = await Db.Addresses.FirstOrDefaultAsync(x => x.Id == _id) as L1Address
                ?? throw new Exception($"Address #{_id} doesn't exist");

            Add(address);
        }

        return address;
    }

    public async Task<L1Address> GetExistingAsync(string hash)
    {
        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses
                .FromSqlRaw("""
                    SELECT *
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "Hash" = {1}
                    """, ChainConfig.Id, hash)
                .FirstOrDefaultAsync() as L1Address
                ?? throw new Exception($"Address {hash} doesn't exist");

            Add(address);
        }

        if (address.Type == AddressType.L1User && address.Balance == 0)
        {
            Db.TryAttach(address);
            address.Counter = Chain.GetManagerCounter();
        }

        return address;
    }

    public async Task<L1Address?> GetAsync(string? hash, L1Block block)
    {
        if (hash is null) return null;

        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses
                .FromSqlRaw("""
                    SELECT *
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "Hash" = {1}
                    """, ChainConfig.Id, hash)
                .FirstOrDefaultAsync() as L1Address
                ?? (hash[0] == 't' && hash[1] == 'z' ? CreateUser(hash, block.Level, block.Timestamp) : null);

            if (address != null) Add(address);
        }

        if (address?.Type == AddressType.L1User && address.Balance == 0)
        {
            Db.TryAttach(address);
            address.Counter = Chain.GetManagerCounter();
        }

        return address;
    }

    public async Task<int?> GetIdOrDefaultAsync(string hash)
    {
        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses
                .FromSqlRaw("""
                    SELECT *
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "Hash" = {1}
                    """, ChainConfig.Id, hash)
                .AsNoTracking()
                .FirstOrDefaultAsync() as L1Address;
        }

        return address?.Id;
    }

    public async Task<L1SmartRollup?> GetSmartRollupOrDefaultAsync(string hash)
    {
        if (!CachedByHash.TryGetValue(hash, out var address))
        {
            address = await Db.Addresses
                .FromSqlRaw("""
                    SELECT *
                    FROM "Addresses"
                    WHERE "ChainId" = {0}
                    AND "Hash" = {1}
                    """, ChainConfig.Id, hash)
                .FirstOrDefaultAsync() as L1Address;

            if (address != null) Add(address);
        }

        return address as L1SmartRollup;
    }

    public bool BakerExists(int id)
    {
        return CachedById.TryGetValue(id, out var address) && address.Type == AddressType.L1Baker;
    }

    public bool BakerExists(string hash)
    {
        return CachedByHash.TryGetValue(hash, out var address) && address.Type == AddressType.L1Baker;
    }

    public L1Baker? GetBaker(int? id)
    {
        if (id is not int _id) return null;

        if (CachedById.TryGetValue(_id, out var address) && address is L1Baker baker)
            return baker;

        throw new Exception($"Unknown baker #{id}");
    }

    public L1Baker GetBaker(int id)
    {
        if (CachedById.TryGetValue(id, out var address) && address is L1Baker baker)
            return baker;

        throw new Exception($"Unknown baker #{id}");
    }

    public L1Baker? GetBaker(string? hash)
    {
        if (hash is null) return null;

        if (CachedByHash.TryGetValue(hash, out var address) && address is L1Baker baker)
            return baker;

        throw new Exception($"Unknown baker '{hash}'");
    }

    public L1Baker GetExistingBaker(string hash)
    {
        if (CachedByHash.TryGetValue(hash, out var address) && address is L1Baker baker)
            return baker;

        throw new Exception($"Unknown baker '{hash}'");
    }

    public L1Baker? GetBakerOrDefault(int? id)
    {
        if (id is not int _id) return null;

        if (CachedById.TryGetValue(_id, out var address) && address is L1Baker baker)
            return baker;

        return null;
    }

    public L1Baker? GetBakerOrDefault(string? hash)
    {
        if (hash is null) return null;

        if (CachedByHash.TryGetValue(hash, out var address) && address is L1Baker baker)
            return baker;

        return null;
    }

    public IEnumerable<L1Baker> GetBakers()
    {
        return CachedById.Values
            .Where(x => x.Type == AddressType.L1Baker)
            .OfType<L1Baker>();
    }

    L1User CreateUser(string hash, int level, DateTime timestamp)
    {
        var address = new L1User
        {
            Id = Chain.NextAddressId(),
            ChainId = ChainConfig.Id,
            Hash = hash,
            FirstLevel = level,
            FirstTimestamp = timestamp,
            LastLevel = level,
            LastTimestamp = timestamp,
        };

        Db.Addresses.Add(address);
        return address;
    }
}
