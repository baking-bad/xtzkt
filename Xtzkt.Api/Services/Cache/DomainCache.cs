using Dapper;
using Npgsql;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Services.Cache;

public class DomainCache
{
    const string Sql = """
        SELECT "Id", "ChainId", "Name", "Address", "Reverse", "Expiration"
        FROM "Domains"
        WHERE "Address" IS NOT NULL
        AND "Expiration" > now()
        """;

    readonly NpgsqlDataSource DataSource;
    readonly ILogger Logger;
    readonly Lock Crit = new();
    readonly SemaphoreSlim Sync = new(1, 1);

    Dictionary<long, CachedDomain> CachedById;
    Dictionary<string, CachedDomain[]> CachedByAddress;
    CachedDomain[]? CachedForSearch;

    public DomainCache(NpgsqlDataSource dataSource, ILogger<DomainCache> logger)
    {
        DataSource = dataSource;
        Logger = logger;

        Logger.LogDebug("Initializing domain cache...");

        using var db = DataSource.OpenConnection();
        (CachedById, CachedByAddress) = Build(db.Query(Sql));

        Logger.LogInformation("Domain cache initialized with {cnt} items", CachedById.Count);
    }

    /// <summary>
    /// Returns the primary domain name of the address (reverse record), if any.
    /// </summary>
    public string? Get(int chainId, string address)
    {
        lock (Crit)
        {
            if (!CachedByAddress.TryGetValue(address, out var domains))
                return null;

            var now = DateTime.UtcNow;
            CachedDomain? matched = null;

            foreach (var domain in domains)
            {
                if (domain.Expiration <= now)
                    continue;

                if (domain.ChainId == chainId)
                    return domain.Name.Original;

                matched ??= domain;
            }

            return matched?.Name.Original;
        }
    }

    /// <summary>
    /// Returns addresses whose domain names match the query, each one with its best matching name.
    /// </summary>
    public (string Address, string Name, double Score)[] Search(string query, int limit)
    {
        CachedDomain[] domains;
        lock (Crit)
        {
            domains = CachedForSearch ??= [.. CachedById.Values];
        }

        var matcher = new FuzzyMatcher(query);
        var matches = new Dictionary<string, (CachedDomain Domain, double Score)>();
        var now = DateTime.UtcNow;

        foreach (var domain in domains)
        {
            if (domain.Expiration <= now)
                continue;

            var score = matcher.Score(domain.Name);
            if (score > 0 && (!matches.TryGetValue(domain.Address, out var best) || ByRelevance.Compare((domain, score), best) < 0))
                matches[domain.Address] = (domain, score);
        }

        return [.. matches.Values
            .Order(ByRelevance)
            .Take(limit)
            .Select(x => (x.Domain.Address, x.Domain.Name.Original, x.Score))];
    }

    /// <summary>
    /// Reloads all the domains.
    /// </summary>
    public async Task ReloadAsync()
    {
        await Sync.WaitAsync();
        try
        {
            await using var db = await DataSource.OpenConnectionAsync();
            var (byId, byAddress) = Build(await db.QueryAsync(Sql));

            lock (Crit)
            {
                CachedById = byId;
                CachedByAddress = byAddress;
                CachedForSearch = null;
            }

            Logger.LogDebug("Domain cache reloaded with {cnt} items", byId.Count);
        }
        finally
        {
            Sync.Release();
        }
    }

    /// <summary>
    /// Reloads the changed domains.
    /// </summary>
    public async Task UpdateAsync(long[] ids)
    {
        await Sync.WaitAsync();
        try
        {
            await using var db = await DataSource.OpenConnectionAsync();
            var domains = (await db.QueryAsync($"""
                {Sql}
                AND "Id" = ANY(@ids)
                """, new { ids })).Select(Parse).ToList();

            var now = DateTime.UtcNow;
            lock (Crit)
            {
                foreach (var id in ids)
                    Remove(id);

                foreach (var domain in domains)
                    Add(domain);

                foreach (var domain in CachedById.Values.Where(x => x.Expiration <= now).ToList())
                    Remove(domain.Id);

                CachedForSearch = null;
            }

            Logger.LogDebug("Domain cache updated with {cnt} changed items", ids.Length);
        }
        finally
        {
            Sync.Release();
        }
    }

    void Add(CachedDomain domain)
    {
        CachedById[domain.Id] = domain;
        if (!domain.Reverse)
            return;

        CachedByAddress[domain.Address] = CachedByAddress.TryGetValue(domain.Address, out var domains)
            ? [.. domains.Append(domain).OrderBy(x => x.Id)]
            : [domain];
    }

    void Remove(long id)
    {
        if (!CachedById.Remove(id, out var domain) || !domain.Reverse)
            return;

        var rest = CachedByAddress[domain.Address].Where(x => x.Id != id).ToArray();
        if (rest.Length == 0)
            CachedByAddress.Remove(domain.Address);
        else
            CachedByAddress[domain.Address] = rest;
    }

    static (Dictionary<long, CachedDomain>, Dictionary<string, CachedDomain[]>) Build(IEnumerable<dynamic> rows)
    {
        var domains = rows.Select(Parse).ToDictionary(x => x.Id);
        return (
            domains,
            domains.Values.Where(x => x.Reverse).GroupBy(x => x.Address).ToDictionary(x => x.Key, x => x.OrderBy(d => d.Id).ToArray()));
    }

    static CachedDomain Parse(dynamic row) => new(
        (long)row.Id,
        (int)row.ChainId,
        (string)row.Address,
        new FuzzyString((string)row.Name),
        (bool)row.Reverse,
        (DateTime)row.Expiration);

    record CachedDomain(long Id, int ChainId, string Address, FuzzyString Name, bool Reverse, DateTime Expiration);

    static readonly Comparer<(CachedDomain Domain, double Score)> ByRelevance = Comparer<(CachedDomain Domain, double Score)>.Create((x, y) =>
    {
        var res = y.Score.CompareTo(x.Score);
        if (res != 0) return res;

        res = x.Domain.Name.Original.Length.CompareTo(y.Domain.Name.Original.Length);
        if (res != 0) return res;

        return x.Domain.Id.CompareTo(y.Domain.Id);
    });
}
