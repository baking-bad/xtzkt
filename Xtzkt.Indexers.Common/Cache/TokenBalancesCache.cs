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
    static Dictionary<(int, HashKey?, long), TokenBalance> Cached = [];

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
        Cached[(tokenBalance.AddressId, HashKey.From(tokenBalance.Entrypoint), tokenBalance.TokenId)] = tokenBalance;
    }

    public void Remove(TokenBalance tokenBalance)
    {
        Cached.Remove((tokenBalance.AddressId, HashKey.From(tokenBalance.Entrypoint), tokenBalance.TokenId));
    }

    public TokenBalance GetOrAdd(TokenBalance tokenBalance)
    {
        if (Cached.TryGetValue((tokenBalance.AddressId, HashKey.From(tokenBalance.Entrypoint), tokenBalance.TokenId), out var res))
            return res;
        Add(tokenBalance);
        return tokenBalance;
    }

    public TokenBalance Get(int addressId, byte[]? entrypoint, long tokenId)
    {
        var _entrypoint = HashKey.From(entrypoint);
        if (!Cached.TryGetValue((addressId, _entrypoint, tokenId), out var tokenBalance))
            throw new Exception($"TokenBalance ({addressId}, {_entrypoint}, {tokenId}) doesn't exist");
        return tokenBalance;
    }

    public bool TryGet(int addressId, byte[]? entrypoint, long tokenId, [NotNullWhen(true)] out TokenBalance? tokenBalance)
    {
        return Cached.TryGetValue((addressId, HashKey.From(entrypoint), tokenId), out tokenBalance);
    }

    public async Task Preload(IEnumerable<(int AddressId, HashKey? Entrypoint, long TokenId)> ids)
    {
        var missed = ids.Where(x => !Cached.ContainsKey(x)).Distinct().ToList();
        if (missed.Count != 0)
        {
            var addressIds = missed.Select(x => x.AddressId).ToArray();
            var entrypoints = missed.Select(x => x.Entrypoint?.Bytes).ToArray();
            var tokenIds = missed.Select(x => x.TokenId).ToArray();

            var items = await Db.TokenBalances
                .FromSqlRaw("""
                    SELECT b.*
                    FROM unnest({0}::int[], {1}::bytea[], {2}::bigint[]) AS q(address, entrypoint, token)
                    INNER JOIN "TokenBalances" b
                    ON b."AddressId" = q.address
                    AND b."TokenId" = q.token
                    AND b."Entrypoint" IS NOT DISTINCT FROM q.entrypoint
                    """, addressIds, entrypoints, tokenIds)
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }
}
