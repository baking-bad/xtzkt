using Dapper;
using Npgsql;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Services.Cache;

public class ProfileCache
{
    readonly NpgsqlDataSource DataSource;
    readonly ILogger Logger;

    readonly Lock Crit = new();
    readonly Dictionary<int, string> CachedById;
    readonly List<(int Id, int ChainId, FuzzyString Name)> CachedForSearch;

    public ProfileCache(NpgsqlDataSource _dataSource, ILogger<ProfileCache> logger)
    {
        DataSource = _dataSource;
        Logger = logger;

        Logger.LogDebug("Initializing profile cache...");

        using var db = DataSource.OpenConnection();
        var profiles = db.Query("""
            SELECT "Id", "ChainId", "Extras"#>>'{profile,alias}' as "Name"
            FROM "Addresses"
            WHERE "Extras"@>'{"profile":{}}' AND "Extras"#>>'{profile,alias}' IS NOT NULL
            """);

        var cap = (int)(profiles.Count() * 1.1);
        CachedById = new(cap);
        CachedForSearch = new(cap);

        foreach (var profile in profiles)
        {
            CachedById.Add((int)profile.Id, (string)profile.Name);
            CachedForSearch.Add(((int)profile.Id, (int)profile.ChainId, new FuzzyString((string)profile.Name)));
        }

        Logger.LogInformation("Profile cache initialized with {cnt} items", CachedById.Count);
    }

    public string? Get(int id)
    {
        lock (Crit)
        {
            return CachedById.GetValueOrDefault(id);
        }
    }

    public (int Id, double Score)[] Search(int[] chains, string query, int limit)
    {
        var matcher = new FuzzyMatcher(query);
        var matches = new List<(int Id, double Score, int Length)>();
        
        foreach (var (id, chainId, name) in CachedForSearch)
        {
            if (!chains.Contains(chainId))
                continue;

            var score = matcher.Score(name);
            if (score > 0)
                matches.Add((id, score, name.Original.Length));
        }

        matches.Sort((x, y) =>
        {
            var res = y.Score.CompareTo(x.Score);
            if (res != 0) return res;

            res = x.Length.CompareTo(y.Length);
            if (res != 0) return res;

            return x.Id.CompareTo(y.Id);
        });

        return [..matches.Take(limit).Select(x => (x.Id, x.Score))];
    }
}
