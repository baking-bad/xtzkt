using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Api.Services.Cache;

public class AddressCache
{
    #region cache
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

        var (codeHash, creatorId) = GetContractProps(address);
        return BuildContractInfo(address, codeHash, GetInfo(creatorId));
    }

    public async Task<Models.ContractInfo> GetContractInfoAsync(int id)
    {
        if (await GetAsync(id) is not Address address)
            throw new Exception("You are lucky :)");

        var (codeHash, creatorId) = GetContractProps(address);
        return BuildContractInfo(address, codeHash, await GetInfoAsync(creatorId));
    }

    static (int CodeHash, int CreatorId) GetContractProps(Address address) => address switch
    {
        L1Contract contract => (contract.CodeHash, contract.CreatorId),
        XEvmContract contract => (contract.CodeHash, contract.CreatorId),
        XMichelsonContract contract => (contract.CodeHash, contract.CreatorId),
        _ => throw new Exception($"Address #{address.Id} is not a contract")
    };

    Models.ContractInfo BuildContractInfo(Address address, int codeHash, Models.AddressInfo creator) => new()
    {
        Id = address.Id,
        Hash = address.Hash,
        Type = Models.Enums.AddressTypes.ToString((int)address.Type),
        Alias = AliasCache.Get(address.Id),
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

    public async Task<List<Address>> GetAsync(List<int> ids)
    {
        await PreloadAsync(ids);
        var res = new List<Address>(ids.Count);
        foreach (var id in ids)
            if (await GetAsync(id) is Address address)
                res.Add(address);
        return res;
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

    public async Task<List<Address>> GetAsync(List<Chain> chains, string hash)
    {
        var res = new List<Address>(chains.Count);
        foreach (var chain in chains)
            if (Compatible(chain, hash) && await GetAsync(chain.Id, hash) is Address address)
                res.Add(address);
        return res;
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

    public async Task<List<Address>> GetAsync(List<Chain> chains, List<string> hashes)
    {
        var res = new List<Address>(hashes.Count * chains.Count);
        foreach (var chain in chains)
            foreach (var hash in hashes)
                if (Compatible(chain, hash) && await GetAsync(chain.Id, hash) is Address address)
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
    #endregion

    #region resolvers
    public async Task<List<Address>> ResolveAddresses(AddressHashEqParameter p, List<Chain> chains)
    {
        var res = new List<Address>();

        if (p.Eq is string eq)
        {
            var addresses = await GetAsync(chains, eq);
            res.AddRange(addresses);
        }
        else if (p.In?.Count > 0)
        {
            var addresses = await GetAsync(chains, p.In);
            res.AddRange(addresses);
        }

        return res;
    }

    public async Task<bool> Resolve(ContractInfoParameter? p, List<Chain> chains)
    {
        if (p == null || p.IsEmpty())
            return true;

        if (chains.Count == 0)
            return false;

        if (!await Resolve(p.Creator, chains))
            return false;

        if (await ResolveAddressId(p.Id, chains) is not (true, var fromId))
            return false;

        if (await ResolveAddressId(p.Hash, chains) is not (true, var fromHash))
            return false;

        if (!Int32Parameter.TryMerge(fromId, fromHash, out var id))
            return false;

        ReduceChains(chains, id);
        if (chains.Count == 0)
            return false;

        p.Id = id;
        p.Hash = null;
        return true;
    }

    public async Task<bool> Resolve(AddressInfoParameter? p, List<Chain> chains)
    {
        if (p == null || p.IsEmpty())
            return true;

        if (chains.Count == 0)
            return false;

        if (await ResolveAddressId(p.Id, chains) is not (true, var fromId))
            return false;

        if (await ResolveAddressId(p.Hash, chains) is not (true, var fromHash))
            return false;

        if (!Int32Parameter.TryMerge(fromId, fromHash, out var id))
            return false;

        ReduceChains(chains, id);
        if (chains.Count == 0)
            return false;

        p.Id = id;
        p.Hash = null;
        return true;
    }

    public async Task<bool> Resolve(AddressInfoNullParameter? p, List<Chain> chains)
    {
        if (p == null || p.IsEmpty())
            return true;

        if (chains.Count == 0)
            return false;

        if (await ResolveAddressId(p.Id, chains) is not (true, var fromId))
            return false;

        if (await ResolveAddressId(p.Hash, chains) is not (true, var fromHash))
            return false;

        if (!Int32NullParameter.TryMerge(fromId, fromHash, out var id))
            return false;

        ReduceChains(chains, id);
        if (chains.Count == 0)
            return false;

        p.Id = id;
        p.Hash = null;
        return true;
    }

    public async Task<(bool, Int32Parameter?)> ResolveAddressId(Int32Parameter? p, List<Chain> chains)
    {
        if (p == null)
            return (true, null);

        if (chains.Count == 0)
            return (false, null);

        var minId = chains.Min(x => IdLayout.MinId32(x.Id));
        var maxId = chains.Max(x => IdLayout.MaxId32(x.Id));
        var res = new Int32Parameter
        {
            Gt = p.Gt == null ? null : Math.Max(p.Gt.Value, minId - 1),
            Ge = p.Ge == null ? null : Math.Max(p.Ge.Value, minId),
            Lt = p.Lt == null ? null : Math.Min(p.Lt.Value, maxId), // should be maxId + 1, but it's ok
            Le = p.Le == null ? null : Math.Min(p.Le.Value, maxId),
        };

        if (p.Eq is int eq)
        {
            if (eq < minId || eq > maxId)
                return (false, null);

            var address = await GetAsync(eq);
            if (address == null || !chains.Any(x => x.Id == address.ChainId))
                return (false, null);

            res.Eq = address.Id;
        }

        if (p.Ne is int ne)
        {
            if (ne >= minId && ne <= maxId)
            {
                var address = await GetAsync(ne);
                if (address != null && chains.Any(x => x.Id == address.ChainId))
                    res.Ne = address.Id;
            }
        }

        if (p.In is List<int> @in)
        {
            var ids = @in.Where(x => x >=  minId && x <= maxId).ToList();
            if (ids.Count == 0)
                return (false, null);

            var addresses = (await GetAsync(ids)).Where(a => chains.Any(x => x.Id == a.ChainId)).ToList();
            if (addresses.Count == 0)
                return (false, null);

            res.In = [.. addresses.Select(x => x.Id)];
        }

        if (p.Ni is List<int> ni)
        {
            var ids = ni.Where(x => x >= minId && x <= maxId).ToList();
            if (ids.Count != 0)
            {
                var addresses = (await GetAsync(ids)).Where(a => chains.Any(x => x.Id == a.ChainId)).ToList();
                if (addresses.Count != 0)
                    res.Ni = [.. addresses.Select(x => x.Id)];
            }
        }

        return (true, res);
    }

    public async Task<(bool, Int32NullParameter?)> ResolveAddressId(Int32NullParameter? p, List<Chain> chains)
    {
        if (p == null)
            return (true, null);

        if (chains.Count == 0)
            return (false, null);

        var minId = chains.Min(x => IdLayout.MinId32(x.Id));
        var maxId = chains.Max(x => IdLayout.MaxId32(x.Id));
        var res = new Int32NullParameter
        {
            Gt = p.Gt is int gt && gt != Int32NullParameter.Null ? Math.Max(gt, minId - 1) : null,
            Ge = p.Ge is int ge && ge != Int32NullParameter.Null ? Math.Max(ge, minId) : null,
            Lt = p.Lt is int lt && lt != Int32NullParameter.Null ? Math.Min(lt, maxId) : null, // should be maxId + 1, but it's ok
            Le = p.Le is int le && le != Int32NullParameter.Null ? Math.Min(le, maxId) : null,
        };

        if (p.Eq is int eq)
        {
            if (eq == Int32NullParameter.Null)
            {
                res.Eq = eq;
            }
            else
            {
                if (eq < minId || eq > maxId)
                    return (false, null);

                var address = await GetAsync(eq);
                if (address == null || !chains.Any(x => x.Id == address.ChainId))
                    return (false, null);

                res.Eq = address.Id;
            }
        }

        if (p.Ne is int ne)
        {
            if (ne == Int32NullParameter.Null)
            {
                res.Ne = ne;
            }
            else if (ne >= minId && ne <= maxId)
            {
                var address = await GetAsync(ne);
                if (address != null && chains.Any(x => x.Id == address.ChainId))
                    res.Ne = address.Id;
            }
        }

        if (p.In is List<int> @in)
        {
            List<int> list = @in.Contains(Int32NullParameter.Null) ? [Int32NullParameter.Null] : [];

            var ids = @in.Where(x => x != Int32NullParameter.Null && x >= minId && x <= maxId).ToList();
            if (ids.Count != 0)
            {
                var addresses = (await GetAsync(ids)).Where(a => chains.Any(x => x.Id == a.ChainId));
                list.AddRange(addresses.Select(x => x.Id));
            }

            if (list.Count == 0)
                return (false, null);

            res.In = list;
        }

        if (p.Ni is List<int> ni)
        {
            List<int> list = ni.Contains(Int32NullParameter.Null) ? [Int32NullParameter.Null] : [];

            var ids = ni.Where(x => x != Int32NullParameter.Null && x >= minId && x <= maxId).ToList();
            if (ids.Count != 0)
            {
                var addresses = (await GetAsync(ids)).Where(a => chains.Any(x => x.Id == a.ChainId));
                list.AddRange(addresses.Select(x => x.Id));
            }

            if (list.Count != 0)
                res.Ni = list;
        }

        return (true, res);
    }

    public async Task<(bool, Int32Parameter?)> ResolveAddressId(AddressHashParameter? p, List<Chain> chains)
    {
        if (p == null)
            return (true, null);

        if (chains.Count == 0)
            return (false, null);

        if (p.Eq is string eq)
        {
            var addresses = await GetAsync(chains, eq);
            if (addresses.Count == 0)
                return (false, null);

            if (addresses.Count == 1)
                return (true, new() { Eq = addresses[0].Id });

            return (true, new() { In = [.. addresses.Select(x => x.Id)] });
        }

        if (p.Ne is string ne)
        {
            var addresses = await GetAsync(chains, ne);
            if (addresses.Count == 0)
                return (true, null);

            if (addresses.Count == 1)
                return (true, new() { Ne = addresses[0].Id });

            return (true, new() { Ni = [.. addresses.Select(x => x.Id)] });
        }

        if (p.In is List<string> @in)
        {
            var addresses = await GetAsync(chains, @in);
            if (addresses.Count == 0)
                return (false, null);

            if (addresses.Count == 1)
                return (true, new() { Eq = addresses[0].Id });

            return (true, new() { In = [.. addresses.Select(x => x.Id)] });
        }

        if (p.Ni is List<string> ni)
        {
            var addresses = await GetAsync(chains, ni);
            if (addresses.Count == 0)
                return (true, null);

            if (addresses.Count == 1)
                return (true, new() { Ne = addresses[0].Id });

            return (true, new() { Ni = [.. addresses.Select(x => x.Id)] });
        }

        return (true, null);
    }

    public async Task<(bool, Int32NullParameter?)> ResolveAddressId(AddressHashNullParameter? p, List<Chain> chains)
    {
        if (p == null)
            return (true, null);

        if (chains.Count == 0)
            return (false, null);

        if (p.Eq is string eq)
        {
            if (eq == AddressHashNullParameter.Null)
                return (true, new() { Eq = Int32NullParameter.Null });

            var addresses = await GetAsync(chains, eq);
            if (addresses.Count == 0)
                return (false, null);

            if (addresses.Count == 1)
                return (true, new() { Eq = addresses[0].Id });

            return (true, new() { In = [.. addresses.Select(x => x.Id)] });
        }

        if (p.Ne is string ne)
        {
            if (ne == AddressHashNullParameter.Null)
                return (true, new() { Ne = Int32NullParameter.Null });

            var addresses = await GetAsync(chains, ne);
            if (addresses.Count == 0)
                return (true, null);

            if (addresses.Count == 1)
                return (true, new() { Ne = addresses[0].Id });

            return (true, new() { Ni = [.. addresses.Select(x => x.Id)] });
        }

        if (p.In is List<string> @in)
        {
            List<int> list = @in.Contains(AddressHashNullParameter.Null) ? [Int32NullParameter.Null] : [];

            var hashes = @in.Where(x => x != AddressHashNullParameter.Null).ToList();
            if (hashes.Count != 0)
            {
                var addresses = await GetAsync(chains, hashes);
                list.AddRange(addresses.Select(x => x.Id));
            }

            if (list.Count == 0)
                return (false, null);

            if (list.Count == 1)
                return (true, new() { Eq = list[0] });

            return (true, new() { In = list });
        }

        if (p.Ni is List<string> ni)
        {
            List<int> list = ni.Contains(AddressHashNullParameter.Null) ? [Int32NullParameter.Null] : [];

            var hashes = ni.Where(x => x != AddressHashNullParameter.Null).ToList();
            if (hashes.Count != 0)
            {
                var addresses = await GetAsync(chains, hashes);
                list.AddRange(addresses.Select(x => x.Id));
            }

            if (list.Count == 0)
                return (true, null);

            if (list.Count == 1)
                return (true, new() { Ne = list[0] });

            return (true, new() { Ni = list });
        }

        return (true, null);
    }

    static void ReduceChains(List<Chain> chains, Int32Parameter? id)
    {
        if (id == null)
            return;

        if (id.Eq is int eq)
        {
            chains.RemoveAll(x => !(eq >= IdLayout.MinId32(x.Id) && eq <= IdLayout.MaxId32(x.Id)));
            return;
        }

        if (id.In is List<int> @in)
        {
            chains.RemoveAll(x => !@in.Any(i => i >= IdLayout.MinId32(x.Id) && i <= IdLayout.MaxId32(x.Id)));
            return;
        }

        if (id.Gt is int gt)
            chains.RemoveAll(x => gt >= IdLayout.MaxId32(x.Id));
        else if (id.Ge is int ge)
            chains.RemoveAll(x => ge > IdLayout.MaxId32(x.Id));

        if (id.Lt is int lt)
            chains.RemoveAll(x => lt <= IdLayout.MinId32(x.Id));
        else if (id.Le is int le)
            chains.RemoveAll(x => le < IdLayout.MinId32(x.Id));
    }

    static void ReduceChains(List<Chain> chains, Int32NullParameter? id)
    {
        if (id == null)
            return;

        if (id.Eq is int eq)
        {
            if (eq != Int32NullParameter.Null)
                chains.RemoveAll(x => !(eq >= IdLayout.MinId32(x.Id) && eq <= IdLayout.MaxId32(x.Id)));
            return;
        }

        if (id.In is List<int> @in)
        {
            if (!@in.Contains(Int32NullParameter.Null))
                chains.RemoveAll(x => !@in.Any(i => i >= IdLayout.MinId32(x.Id) && i <= IdLayout.MaxId32(x.Id)));
            return;
        }

        if (id.Gt is int gt && gt != Int32NullParameter.Null)
            chains.RemoveAll(x => gt >= IdLayout.MaxId32(x.Id));
        else if (id.Ge is int ge && ge != Int32NullParameter.Null)
            chains.RemoveAll(x => ge > IdLayout.MaxId32(x.Id));

        if (id.Lt is int lt && lt != Int32NullParameter.Null)
            chains.RemoveAll(x => lt <= IdLayout.MinId32(x.Id));
        else if (id.Le is int le && le != Int32NullParameter.Null)
            chains.RemoveAll(x => le < IdLayout.MinId32(x.Id));
    }
    #endregion
}
