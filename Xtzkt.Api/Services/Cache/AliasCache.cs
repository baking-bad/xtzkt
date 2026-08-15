using Dapper;
using Npgsql;
using Xtzkt.Api.Utils;

namespace Xtzkt.Api.Services.Cache;

public class AliasCache
{
    readonly NpgsqlDataSource DataSource;
    readonly ILogger Logger;

    readonly Lock Crit = new();
    readonly Dictionary<int, string> CachedById;
    readonly List<(int Id, int ChainId, FuzzyString Alias)> CachedForSearch;

    public AliasCache(NpgsqlDataSource _dataSource, ILogger<AliasCache> logger)
    {
        DataSource = _dataSource;
        Logger = logger;

        Logger.LogDebug("Initializing alias cache...");

        using var db = DataSource.OpenConnection();
        var aliases = db.Query("""
            SELECT "Id", "ChainId", "Extras"#>>'{profile,alias}' as "Alias"
            FROM "Addresses"
            WHERE "Extras"@>'{"profile":{}}' AND "Extras"#>>'{profile,alias}' IS NOT NULL
            """);

        var cap = (int)(aliases.Count() * 1.1);
        CachedById = new(cap);
        CachedForSearch = new(cap);

        foreach (var alias in aliases)
        {
            CachedById.Add((int)alias.Id, (string)alias.Alias);
            CachedForSearch.Add(((int)alias.Id, (int)alias.ChainId, new FuzzyString((string)alias.Alias)));
        }

        Logger.LogInformation("Alias cache initialized with {cnt} items", CachedById.Count);
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
        
        foreach (var (id, chainId, alias) in CachedForSearch)
        {
            if (!chains.Contains(chainId))
                continue;

            var score = matcher.Score(alias);
            if (score > 0)
                matches.Add((id, score, alias.Original.Length));
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
