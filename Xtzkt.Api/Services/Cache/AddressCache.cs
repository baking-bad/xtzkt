using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class AddressCache
{
    readonly ChainCache ChainCache;
    readonly AliasCache AliasCache;
    readonly IDbContextFactory<XtzktContext> DbFactory;
    readonly ILogger Logger;
    readonly int HardLimit;
    readonly int SoftLimit;

    readonly Lock Crit = new();
    readonly Dictionary<int, Address> CachedById = [];
    readonly Dictionary<string, Address>[] CachedByHash = [[], [], [], [], [], [], [], []];
    readonly int[] LastLevels = new int[8];

    public AddressCache(
        ChainCache chainCache,
        AliasCache aliasCache,
        IDbContextFactory<XtzktContext> dbFactory,
        IConfiguration config,
        ILogger<AddressCache> logger)
    {
        ChainCache = chainCache;
        AliasCache = aliasCache;
        DbFactory = dbFactory;
        Logger = logger;

        Logger.LogDebug("Initializing address cache...");

        var chains = chainCache.Get();
        foreach (var chain in chains)
            LastLevels[chain.Id] = chain.Level;

        var totalAddresses = chains.Sum(x => x.AddressCounter);
        var limits = config.GetCacheConfig().Address;
        HardLimit = limits.GetHardLimit(totalAddresses);
        SoftLimit = Math.Min(limits.GetSoftLimit(totalAddresses), HardLimit);

        using var db = DbFactory.CreateDbContext();
        var addresses = db.Addresses.AsQueryable();
        if (SoftLimit != 0) addresses = addresses.OrderByDescending(x => x.Id).Take(SoftLimit);
        foreach (var address in addresses)
        {
            CachedById.Add(address.Id, address);
            CachedByHash[address.ChainId].Add(address.Hash, address);
        }

        Logger.LogInformation("Address cache initialized with {cnt} items", CachedById.Count);
    }

    public async Task OnStateChanged(int chainId, int minLevel, int lastLevel)
    {
        var cacheLevel = LastLevels[chainId];
        var lastValidLevel = Math.Min(cacheLevel, minLevel - 1);

        if (minLevel <= cacheLevel)
        {
            List<Address> reorged;
            lock (Crit)
            {
                reorged = [..CachedById.Values.Where(x => x.ChainId == chainId && x.LastLevel >= minLevel)];
                foreach (var address in reorged)
                {
                    CachedById.Remove(address.Id);
                    CachedByHash[address.ChainId].Remove(address.Hash);
                }
            }
            Logger.LogDebug("Removed {cnt} reorged addresses for chain #{chainId}", reorged.Count, chainId);
        }

        using var db = DbFactory.CreateDbContext();
        var updated = await db.Addresses
            .Where(x => x.ChainId == chainId && x.LastLevel > lastValidLevel)
            .ToListAsync();

        var toUpdate = updated.AsEnumerable();
        lock (Crit)
        {
            if (HardLimit != 0)
                toUpdate = toUpdate.Where(x => CachedById.ContainsKey(x.Id));

            foreach (var address in toUpdate)
            {
                CachedById[address.Id] = address;
                CachedByHash[address.ChainId][address.Hash] = address;
            }
        }
        
        Logger.LogDebug("Updated {cnt} addresses for chain #{chainId}", updated.Count, chainId);
        LastLevels[chainId] = lastLevel;
    }

    public Models.AddressInfo? GetInfo(int? id)
    {
        if (id is not int _id || Get(_id) is not Address address)
            return null;

        return new()
        {
            Id = address.Id,
            Hash = address.Hash,
            Type = Models.Enums.AddressTypes.ToString((int)address.Type),
            Alias = AliasCache.Get(_id),
        };
    }

    public async Task<Models.AddressInfo?> GetInfoAsync(int? id)
    {
        if (id is not int _id || await GetAsync(_id) is not Address address)
            return null;

        return new()
        {
            Id = address.Id,
            Hash = address.Hash,
            Type = Models.Enums.AddressTypes.ToString((int)address.Type),
            Alias = AliasCache.Get(_id),
        };
    }

    public Models.AddressInfo GetInfo(int id)
    {
        if (Get(id) is not Address address)
            throw new Exception("You are lucky :)");

        return new()
        {
            Id = address.Id,
            Hash = address.Hash,
            Type = Models.Enums.AddressTypes.ToString((int)address.Type),
            Alias = AliasCache.Get(id),
        };
    }

    public async Task<Models.AddressInfo> GetInfoAsync(int id)
    {
        if (await GetAsync(id) is not Address address)
            throw new Exception("You are lucky :)");

        return new()
        {
            Id = address.Id,
            Hash = address.Hash,
            Type = Models.Enums.AddressTypes.ToString((int)address.Type),
            Alias = AliasCache.Get(id),
        };
    }

    public Models.ContractInfo GetContractInfo(int id)
    {
        if (Get(id) is not Address address)
            throw new Exception("You are lucky :)");

        var (typeHash, codeHash, creatorId) = GetContractProps(address);
        return BuildContractInfo(address, typeHash, codeHash, GetInfo(creatorId));
    }

    public async Task<Models.ContractInfo> GetContractInfoAsync(int id)
    {
        if (await GetAsync(id) is not Address address)
            throw new Exception("You are lucky :)");

        var (typeHash, codeHash, creatorId) = GetContractProps(address);
        return BuildContractInfo(address, typeHash, codeHash, await GetInfoAsync(creatorId));
    }

    static (int TypeHash, int CodeHash, int CreatorId) GetContractProps(Address address) => address switch
    {
        L1Contract contract => (contract.TypeHash, contract.CodeHash, contract.CreatorId),
        XEvmContract contract => (contract.TypeHash, contract.CodeHash, contract.CreatorId),
        XMichelsonContract contract => (contract.TypeHash, contract.CodeHash, contract.CreatorId),
        _ => throw new Exception($"Address #{address.Id} is not a contract")
    };

    Models.ContractInfo BuildContractInfo(Address address, int typeHash, int codeHash, Models.AddressInfo creator) => new()
    {
        Id = address.Id,
        Hash = address.Hash,
        Type = Models.Enums.AddressTypes.ToString((int)address.Type),
        Alias = AliasCache.Get(address.Id),
        TypeHash = typeHash,
        CodeHash = codeHash,
        Creator = creator,
    };

    public Address? Get(int id)
    {
        if (!TryGetSafe(id, out var address) && HardLimit != 0)
        {
            using var db = DbFactory.CreateDbContext();
            address = db.Addresses.FirstOrDefault(x => x.Id == id);
            if (address != null) Add(address);
        }
        return address;
    }

    public async Task<Address?> GetAsync(int id)
    {
        if (!TryGetSafe(id, out var address) && HardLimit != 0)
        {
            using var db = DbFactory.CreateDbContext();
            address = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id);
            if (address != null) Add(address);
        }
        return address;
    }

    public Address? Get(int chainId, string hash)
    {
        if (!TryGetSafe(chainId, hash, out var address) && HardLimit != 0)
        {
            using var db = DbFactory.CreateDbContext();
            address = db.Addresses.FirstOrDefault(x => x.ChainId == chainId && x.Hash == hash);
            if (address != null) Add(address);
        }
        return address;
    }

    public async Task<Address?> GetAsync(int chainId, string hash)
    {
        if (!TryGetSafe(chainId, hash, out var address) && HardLimit != 0)
        {
            using var db = DbFactory.CreateDbContext();
            address = await db.Addresses.FirstOrDefaultAsync(x => x.ChainId == chainId && x.Hash == hash);
            if (address != null) Add(address);
        }
        return address;
    }

    public async Task<List<Address>> GetAsync(string hash)
    {
        var chains = ChainCache.Get();
        var res = new List<Address>(chains.Count);
        foreach (var chain in chains)
            if (Compatible(chain, hash) && await GetAsync(chain.Id, hash) is Address address)
                res.Add(address);
        return res;
    }

    public async Task<List<Address>> GetAsync(int chainId, List<string> hashes)
    {
        var res = new List<Address>(hashes.Count);
        foreach (var hash in hashes)
            if (await GetAsync(chainId, hash) is Address address)
                res.Add(address);
        return res;
    }

    public async Task<List<Address>> GetAsync(List<string> hashes)
    {
        var chains = ChainCache.Get();
        var res = new List<Address>(hashes.Count * chains.Count);
        foreach (var chain in chains)
            foreach (var hash in hashes)
                if (Compatible(chain, hash) && await GetAsync(chain.Id, hash) is Address address)
                    res.Add(address);
        return res;
    }

    static bool Compatible(Chain chain, string hash)
    {
        return chain.Layer == Layer.TezosX || !hash.StartsWith("0x", StringComparison.Ordinal);
    }

    public async Task PreloadAsync(IEnumerable<int> ids)
    {
        HashSet<int> missed;
        lock (Crit)
        {
            missed = ids.Where(x => !CachedById.ContainsKey(x)).ToHashSet();
        }

        if (missed.Count != 0)
        {
            using var db = DbFactory.CreateDbContext();
            var addresses = await db.Addresses
                .Where(x => missed.Contains(x.Id))
                .ToListAsync();

            foreach (var address in addresses)
                Add(address);
        }
    }

    public async Task PreloadAsync(IEnumerable<int?> ids)
    {
        HashSet<int?> missed;
        lock (Crit)
        {
            missed = ids.Where(x => x is int id && !CachedById.ContainsKey(id)).ToHashSet();
        }

        if (missed.Count != 0)
        {
            using var db = DbFactory.CreateDbContext();
            var addresses = await db.Addresses
                .Where(x => missed.Contains(x.Id))
                .ToListAsync();

            foreach (var address in addresses)
                Add(address);
        }
    }

    bool TryGetSafe(int id, [NotNullWhen(true)] out Address? address)
    {
        lock (Crit)
        {
            return CachedById.TryGetValue(id, out address);
        }
    }

    bool TryGetSafe(int chainId, string hash, [NotNullWhen(true)] out Address? address)
    {
        lock (Crit)
        {
            return CachedByHash[chainId].TryGetValue(hash, out address);
        }
    }

    void Add(Address address)
    {
        lock (Crit)
        {
            #region check limits
            if (HardLimit != 0 && CachedById.Count >= HardLimit)
            {
                Logger.LogDebug("Cache is full. Clearing...");
                var toRemove = CachedById.Values
                    .Take(CachedById.Count / 4)
                    .ToList();

                foreach (var addr in toRemove)
                {
                    CachedById.Remove(addr.Id);
                    CachedByHash[addr.ChainId].Remove(addr.Hash);
                }
                Logger.LogDebug("Removed {cnt} addresses", toRemove.Count);
            }
            #endregion

            CachedById[address.Id] = address;
            CachedByHash[address.ChainId][address.Hash] = address;
        }
        Logger.LogDebug("Address {hash} cached", address.Hash);
    }

}
