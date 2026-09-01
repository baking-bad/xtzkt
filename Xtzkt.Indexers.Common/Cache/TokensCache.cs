using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class TokensCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<long, Token> CachedById = [];
    static Dictionary<(int, BigInteger), Token> CachedByKey = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 100_000;
        TargetCap = size?.TargetCap ?? 80_000;
        CachedById = new(SoftCap + 4096);
        CachedByKey = new(SoftCap + 4096);
    }
    #endregion

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public void Reset()
    {
        CachedById.Clear();
        CachedByKey.Clear();
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

    public void Add(Token token)
    {
        CachedById[token.Id] = token;
        CachedByKey[(token.ContractId, token.TokenId)] = token;
    }

    public void Remove(Token token)
    {
        CachedById.Remove(token.Id);
        CachedByKey.Remove((token.ContractId, token.TokenId));
    }

    public Token Get(long id)
    {
        if (!CachedById.TryGetValue(id, out var token))
            throw new Exception($"Token #{id} doesn't exist");
        return token;
    }

    public bool TryGet(int contractId, BigInteger tokenId, [NotNullWhen(true)] out Token? token)
    {
        return CachedByKey.TryGetValue((contractId, tokenId), out token);
    }

    public async Task Preload(IEnumerable<long> ids)
    {
        var missed = ids.Where(x => !CachedById.ContainsKey(x)).ToHashSet();
        if (missed.Count != 0)
        {
            var items = await Db.Tokens
                .Where(x => missed.Contains(x.Id))
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }

    public async Task Preload(IEnumerable<(int, BigInteger)> ids)
    {
        var missed = ids.Where(x => !CachedByKey.ContainsKey(x)).Distinct().ToList();
        if (missed.Count != 0)
        {
            var contractIds = missed.Select(x => x.Item1).ToArray();
            var tokenIds = missed.Select(x => x.Item2).ToArray();

            var items = await Db.Tokens
                .FromSqlRaw("""
                    SELECT t.*
                    FROM unnest({0}::int[], {1}::numeric[]) AS q(contract, token)
                    INNER JOIN "Tokens" t ON t."ContractId" = q.contract AND t."TokenId" = q.token
                    """, contractIds, tokenIds)
                .ToListAsync();

            foreach (var item in items)
                Add(item);
        }
    }
}
