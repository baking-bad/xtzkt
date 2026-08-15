using Npgsql;
using System.Numerics;
using Xtzkt.Data.Models;
using Xtzkt.Services.Metadata.Models;

namespace Xtzkt.Services.Metadata.Services;

public sealed class StoreService(NpgsqlDataSource dataSource)
{
    public async Task EnsureEvmResolverIndexes(CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand($"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "MX_Tokens_ChainId_MetadataStatus_Id:EvmResolver"
            ON "Tokens" ("ChainId", "MetadataStatus", "Id")
            WHERE ("Tags" & {(int)TokenTags.Erc}) = {(int)TokenTags.Erc}
            AND "MetadataStatus" <= {(int)TokenMetadataStatus.MaxRetry}
            AND "MetadataLink" IS NULL
            """);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EnsureIpfsResolverIndexes(CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand($"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "MX_Tokens_MetadataStatus_Id:IpfsResolver"
            ON "Tokens" ("MetadataStatus", "Id")
            WHERE "MetadataLink" IS NOT NULL
            AND "MetadataLink" ^@ 'ipfs'
            AND "MetadataStatus" <= {(int)TokenMetadataStatus.MaxRetry}
            """);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EnsureHttpResolverIndexes(CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand($"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "MX_Tokens_MetadataStatus_Id:HttpResolver"
            ON "Tokens" ("MetadataStatus", "Id")
            WHERE "MetadataLink" IS NOT NULL
            AND "MetadataLink" ^@ 'http'
            AND "MetadataStatus" <= {(int)TokenMetadataStatus.MaxRetry}
            """);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ChainInfo>> GetChainsAsync(CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(@"SELECT ""Id"", ""ChainId"" FROM ""Chains""");

        var chains = new List<ChainInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            chains.Add(new ChainInfo(reader.GetInt32(0), reader.GetString(1)));

        return chains;
    }

    public async Task<List<TokenInfo>> GetEvmPendingTokensAsync(int chainId, int limit, long[] exclude, int[] retryDelays, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT t."Id", c."Hash", t."TokenId"::text, t."Tags", t."MetadataStatus"
            FROM "Tokens" AS t
            JOIN "Addresses" c ON c."Id" = t."ContractId"
            WHERE (t."Tags" & {(int)TokenTags.Erc}) = {(int)TokenTags.Erc}
            AND "MetadataStatus" <= {(int)TokenMetadataStatus.MaxRetry}
            AND "MetadataLink" IS NULL
            AND t."ChainId" = @chainId
            AND (t."MetadataSyncedAt" IS NULL OR t."MetadataSyncedAt" < @now - COALESCE((@delays::int[])[t."MetadataStatus"], 0) * interval '1 second')
            AND NOT (t."Id" = ANY(@exclude))
            ORDER BY t."MetadataStatus", t."Id"
            LIMIT @limit
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("chainId", chainId);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("delays", retryDelays);
        cmd.Parameters.AddWithValue("exclude", exclude);
        cmd.Parameters.AddWithValue("limit", limit);

        var tokens = new List<TokenInfo>(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tokens.Add(new TokenInfo(
                Id: reader.GetInt64(0),
                Contract: reader.GetString(1),
                TokenId: BigInteger.Parse(reader.GetString(2)),
                Tags: (TokenTags)reader.GetInt32(3),
                Status: (TokenMetadataStatus)reader.GetInt32(4)));
        }

        return tokens;
    }

    public async Task<List<TokenLinkInfo>> GetPendingLinksAsync(string protocol, int limit, long[] exclude, int[] retryDelays, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT t."Id", t."MetadataLink", t."TokenId"::text, t."Tags", t."MetadataStatus"
            FROM "Tokens" AS t
            WHERE "MetadataLink" IS NOT NULL
            AND "MetadataLink" ^@ '{protocol}'
            AND "MetadataStatus" <= {(int)TokenMetadataStatus.MaxRetry}
            AND (t."MetadataSyncedAt" IS NULL OR t."MetadataSyncedAt" < @now - COALESCE((@delays::int[])[t."MetadataStatus"], 0) * interval '1 second')
            AND NOT (t."Id" = ANY(@exclude))
            ORDER BY t."MetadataStatus", t."Id"
            LIMIT @limit
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("delays", retryDelays);
        cmd.Parameters.AddWithValue("exclude", exclude);
        cmd.Parameters.AddWithValue("limit", limit);

        var tokens = new List<TokenLinkInfo>(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tokens.Add(new TokenLinkInfo(
                Id: reader.GetInt64(0),
                Link: reader.GetString(1),
                TokenId: BigInteger.Parse(reader.GetString(2)),
                Tags: (TokenTags)reader.GetInt32(3),
                Status: (TokenMetadataStatus)reader.GetInt32(4)));
        }

        return tokens;
    }

    public async Task<int> SaveAsync(List<TokenMetadata> results, CancellationToken ct = default)
    {
        if (results.Count == 0) return 0;

        var ids = new long[results.Count];
        var metas = new string?[results.Count];
        var names = new string?[results.Count];
        var symbols = new string?[results.Count];
        var decimals = new int?[results.Count];
        var links = new string?[results.Count];
        var statuses = new int[results.Count];
        var times = new DateTime?[results.Count];

        var i = 0;
        foreach (var r in results)
        {
            ids[i] = r.Id;
            metas[i] = r.Json;
            names[i] = r.Name;
            symbols[i] = r.Symbol;
            decimals[i] = r.Decimals;
            links[i] = r.Link;
            statuses[i] = (int)r.Status;
            times[i] = r.SyncedAt;
            i++;
        }

        const string sql = """
            UPDATE "Tokens" AS t
            SET "Metadata" = COALESCE(v.meta::jsonb, t."Metadata"),
                "Name" = COALESCE(v.name, t."Name"),
                "Symbol" = COALESCE(v.symbol, t."Symbol"),
                "Decimals" = COALESCE(v.decimals, t."Decimals"),
                "MetadataLink" = COALESCE(v.link, t."MetadataLink"),
                "MetadataStatus" = v.status,
                "MetadataSyncedAt" = v.time
            FROM unnest(@ids, @metas, @names, @symbols, @decimals, @links, @statuses, @times) AS v(id, meta, name, symbol, decimals, link, status, time)
            WHERE t."Id" = v.id
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("ids", ids);
        cmd.Parameters.AddWithValue("metas", metas);
        cmd.Parameters.AddWithValue("names", names);
        cmd.Parameters.AddWithValue("symbols", symbols);
        cmd.Parameters.AddWithValue("decimals", decimals);
        cmd.Parameters.AddWithValue("links", links);
        cmd.Parameters.AddWithValue("statuses", statuses);
        cmd.Parameters.AddWithValue("times", times);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    #region dipdup
    public async Task EnsureDipDupResolverIndexes(CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand($"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "MX_Tokens_ChainId_MetadataStatus_Id:DipDupResolver"
            ON "Tokens" ("ChainId", "MetadataStatus", "Id")
            WHERE ("Tags" & {(int)TokenTags.Fa}) = {(int)TokenTags.Fa}
            AND "MetadataStatus" <= {(int)TokenMetadataStatus.MaxRetry}
            """);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string?> GetDipDupResolverStateAsync(int chainId, CancellationToken ct = default)
    {
        var sql = """
            SELECT ("Extras"#>'{metadata,dipdup}')::text
            FROM "Chains"
            WHERE "Id" = @id
            LIMIT 1
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", chainId);

        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    public async Task<int> SaveDipDupResolverStateAsync(int chainId, string json, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "Chains"
            SET "Extras" = jsonb_set(
                jsonb_set(
                    COALESCE("Extras", '{}'),
                    '{metadata}',
                    COALESCE("Extras" -> 'metadata', '{}')
                ),
                '{metadata,dipdup}',
                @state::jsonb
            )
            WHERE "Id" = @id
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", chainId);
        cmd.Parameters.AddWithValue("state", json);
        
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Dictionary<string, int>> GetContractIdsAsync(int chainId, string[] hashes, CancellationToken ct = default)
    {
        var sql = """
            SELECT "Id", "Hash"
            FROM "Addresses"
            WHERE "ChainId" = @chainId
            AND "Hash" = ANY(@hashes)
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("chainId", chainId);
        cmd.Parameters.AddWithValue("hashes", hashes);

        var map = new Dictionary<string, int>(hashes.Length);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            map[reader.GetString(1)] = reader.GetInt32(0);

        return map;
    }

    public async Task<List<TokenInfo>> GetPendingFaTokensAsync(int chainId, int limit, int[] retryDelays, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT t."Id", c."Hash", t."TokenId"::text, t."Tags", t."MetadataStatus"
            FROM "Tokens" AS t
            JOIN "Addresses" c ON c."Id" = t."ContractId"
            WHERE (t."Tags" & {(int)TokenTags.Fa}) = {(int)TokenTags.Fa}
            AND t."MetadataStatus" <= {(int)TokenMetadataStatus.MaxRetry}
            AND (t."MetadataSyncedAt" IS NULL OR t."MetadataSyncedAt" < @now - COALESCE((@delays::int[])[t."MetadataStatus"], 0) * interval '1 second')
            AND t."ChainId" = @chainId
            ORDER BY t."MetadataStatus", t."Id"
            LIMIT @limit
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("chainId", chainId);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("delays", retryDelays);
        cmd.Parameters.AddWithValue("limit", limit);

        var tokens = new List<TokenInfo>(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tokens.Add(new TokenInfo(
                Id: reader.GetInt64(0),
                Contract: reader.GetString(1),
                TokenId: BigInteger.Parse(reader.GetString(2)),
                Tags: (TokenTags)reader.GetInt32(3),
                Status: (TokenMetadataStatus)reader.GetInt32(4)));
        }

        return tokens;
    }

    public async Task<int> SaveAsync(List<TokenMetadataEx> results, CancellationToken ct = default)
    {
        if (results.Count == 0) return 0;

        var contracts = new int[results.Count];
        var tokens = new string[results.Count];
        var metas = new string?[results.Count];
        var names = new string?[results.Count];
        var symbols = new string?[results.Count];
        var decimals = new int?[results.Count];
        var links = new string?[results.Count];
        var statuses = new int[results.Count];
        var times = new DateTime?[results.Count];

        var i = 0;
        foreach (var r in results)
        {
            contracts[i] = r.ContractId;
            tokens[i] = r.TokenId;
            metas[i] = r.Json;
            names[i] = r.Name;
            symbols[i] = r.Symbol;
            decimals[i] = r.Decimals;
            links[i] = r.Link;
            statuses[i] = (int)r.Status;
            times[i] = r.SyncedAt;
            i++;
        }

        const string sql = """
            UPDATE "Tokens" AS t
            SET "Metadata" = v.meta::jsonb,
                "Name" = v.name,
                "Symbol" = v.symbol,
                "Decimals" = v.decimals,
                "MetadataLink" = v.link,
                "MetadataStatus" = v.status,
                "MetadataSyncedAt" = v.time
            FROM unnest(@contracts, @tokens, @metas, @names, @symbols, @decimals, @links, @statuses, @times) AS v(contract, token, meta, name, symbol, decimals, link, status, time)
            WHERE t."ContractId" = v.contract AND t."TokenId" = v.token::numeric
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("contracts", contracts);
        cmd.Parameters.AddWithValue("tokens", tokens);
        cmd.Parameters.AddWithValue("metas", metas);
        cmd.Parameters.AddWithValue("names", names);
        cmd.Parameters.AddWithValue("symbols", symbols);
        cmd.Parameters.AddWithValue("decimals", decimals);
        cmd.Parameters.AddWithValue("links", links);
        cmd.Parameters.AddWithValue("statuses", statuses);
        cmd.Parameters.AddWithValue("times", times);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> SaveDipDupContractMetadataAsync(int chainId, List<(string Hash, string? Json)> contracts, CancellationToken ct = default)
    {
        if (contracts.Count == 0) return 0;

        var hashes = new string[contracts.Count];
        var metas = new string?[contracts.Count];

        var i = 0;
        foreach (var (hash, json) in contracts)
        {
            hashes[i] = hash;
            metas[i] = json;
            i++;
        }

        const string sql = """
            UPDATE "Addresses" AS a
            SET "Metadata" = v.meta::jsonb
            FROM unnest(@hashes, @metas) AS v(hash, meta)
            WHERE a."ChainId" = @chainId AND a."Hash" = v.hash
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("chainId", chainId);
        cmd.Parameters.AddWithValue("hashes", hashes);
        cmd.Parameters.AddWithValue("metas", metas);

        return await cmd.ExecuteNonQueryAsync(ct);
    }
    #endregion
}
