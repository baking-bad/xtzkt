using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class TokenBalancesCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<(int, HashableBytes?, long), TokenBalance> Cached = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 120_000;
        TargetCap = size?.TargetCap ?? 100_000;
        Cached = new(SoftCap + 4096);
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

    public void Add(TokenBalance tokenBalance)
    {
        Cached[(tokenBalance.AddressId, HashableBytes.From(tokenBalance.Entrypoint), tokenBalance.TokenId)] = tokenBalance;
    }

    public void Remove(TokenBalance tokenBalance)
    {
        Cached.Remove((tokenBalance.AddressId, HashableBytes.From(tokenBalance.Entrypoint), tokenBalance.TokenId));
    }

    public TokenBalance GetOrAdd(TokenBalance tokenBalance)
    {
        if (Cached.TryGetValue((tokenBalance.AddressId, HashableBytes.From(tokenBalance.Entrypoint), tokenBalance.TokenId), out var res))
            return res;
        Add(tokenBalance);
        return tokenBalance;
    }

    public TokenBalance Get(int addressId, byte[]? entrypoint, long tokenId)
    {
        var _entrypoint = HashableBytes.From(entrypoint);
        if (!Cached.TryGetValue((addressId, _entrypoint, tokenId), out var tokenBalance))
            throw new Exception($"TokenBalance ({addressId}, {_entrypoint}, {tokenId}) doesn't exist");
        return tokenBalance;
    }

    public bool TryGet(int addressId, byte[]? entrypoint, long tokenId, [NotNullWhen(true)] out TokenBalance? tokenBalance)
    {
        return Cached.TryGetValue((addressId, HashableBytes.From(entrypoint), tokenId), out tokenBalance);
    }

    public async Task Preload(IEnumerable<(int AddressId, HashableBytes? Entrypoint, long TokenId)> ids)
    {
        var missed = ids.Where(x => !Cached.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            for (int i = 0, n = 2048; i < missed.Count; i += n)
            {
                var corteges1 = string.Join(',', missed.Skip(i).Take(n).Where(x => x.Entrypoint == null).Select(x => $"({x.AddressId}, {x.TokenId})"));
                var corteges2 = string.Join(',', missed.Skip(i).Take(n).Where(x => x.Entrypoint != null).Select(x => $"({x.AddressId}, '\\x{x.Entrypoint}', {x.TokenId})"));
                string query;

                if (corteges1.Length != 0)
                {
                    if (corteges2.Length != 0)
                    {
                        query = $"""
                            SELECT *
                            FROM "TokenBalances"
                            WHERE ("AddressId", "TokenId") IN ({corteges1}) AND "Entrypoint" IS NULL
                                    
                            UNION ALL
                                    
                            SELECT *
                            FROM "TokenBalances"
                            WHERE ("AddressId", "Entrypoint", "TokenId") IN ({corteges2})
                            """;
                    }
                    else
                    {
                        query = $"""
                            SELECT *
                            FROM "TokenBalances"
                            WHERE ("AddressId", "TokenId") IN ({corteges1}) AND "Entrypoint" IS NULL
                            """;
                    }
                }
                else
                {
                    query = $"""
                        SELECT *
                        FROM "TokenBalances"
                        WHERE ("AddressId", "Entrypoint", "TokenId") IN ({corteges2})
                        """;
                }

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
                var items = await Db.TokenBalances
                    .FromSqlRaw(query)
                    .ToListAsync();
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

                foreach (var item in items)
                    Add(item);
            }
        }
    }
}
