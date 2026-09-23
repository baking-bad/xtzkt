using Npgsql;
using Xtzkt.Services.Domains.Models;

namespace Xtzkt.Services.Domains.Services;

public sealed class StoreService(NpgsqlDataSource dataSource)
{
    static readonly DateTime MaxDateTime = DateTimeOffset.MaxValue.UtcDateTime.Date;

    // Netezos renders a timestamp as ISO 8601 while it fits DateTime, and as a raw number beyond that
    const string ExpirationSql = """
        CASE WHEN expiry."JsonValue" #>> '{}' ~ '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$' AND pg_input_is_valid(expiry."JsonValue" #>> '{}', 'timestamptz')
            THEN (expiry."JsonValue" #>> '{}')::timestamptz
            ELSE @maxDateTime
        END
        """;

    readonly SemaphoreSlim _sema = new(1, 1);

    #region chains
    public async Task<HashSet<int>> GetChainIdsAsync(CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT "Id" FROM "Chains"
            """);

        var res = new HashSet<int>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            res.Add(reader.GetInt32(0));

        return res;
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        return dataSource.OpenConnectionAsync(ct);
    }

    public async Task<BlockInfo?> GetHeadAsync(NpgsqlConnection conn, int chainId, CancellationToken ct = default)
    {
        // deliberately fetching from "Blocks" to avoid deadlocks on "Chains"
        await using var cmd = new NpgsqlCommand("""
            SELECT "Level", "Hash"
            FROM "Blocks"
            WHERE "ChainId" = @chain
            ORDER BY "Level" DESC
            LIMIT 1
            """, conn);
        cmd.Parameters.AddWithValue("chain", chainId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new BlockInfo(
            ChainId: chainId,
            Level: reader.GetInt32(0),
            Hash: reader.GetFieldValue<byte[]>(1));
    }

    public async Task<bool> IsValidBranchAsync(NpgsqlConnection conn, BlockInfo block, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1
                FROM "Blocks"
                WHERE "ChainId" = @chain
                AND "Level" = @level
                AND "Hash" = @hash
            )
            """, conn);
        cmd.Parameters.AddWithValue("chain", block.ChainId);
        cmd.Parameters.AddWithValue("level", block.Level);
        cmd.Parameters.AddWithValue("hash", block.Hash);

        return await cmd.ExecuteScalarAsync(ct) is true;
    }
    #endregion

    #region registry
    public async Task<RegistryInfo?> GetRegistryAsync(int chainId, string hash, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT a."Id",
                   MAX(b."Id") FILTER (WHERE b."StoragePath" = 'store.records'),
                   MAX(b."Id") FILTER (WHERE b."StoragePath" = 'store.reverse_records'),
                   MAX(b."Id") FILTER (WHERE b."StoragePath" = 'store.expiry_map')
            FROM "Addresses" AS a
            LEFT JOIN "BigMaps" AS b ON b."ContractId" = a."Id" AND b."Active"
            WHERE a."ChainId" = @chain
            AND a."Hash" = @hash
            GROUP BY a."Id"
            LIMIT 1
            """);
        cmd.Parameters.AddWithValue("chain", chainId);
        cmd.Parameters.AddWithValue("hash", hash);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3)) return null;

        return new RegistryInfo(
            Id: reader.GetInt32(0),
            ChainId: chainId,
            Hash: hash,
            RecordsBigMap: reader.GetInt32(1),
            ReverseBigMap: reader.GetInt32(2),
            ExpiryBigMap: reader.GetInt32(3));
    }

    public async Task EnsureIndexesAsync(RegistryInfo registry, CancellationToken ct = default)
    {
        await _sema.WaitAsync(ct);
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await ExecuteAsync(conn, "SET statement_timeout = 0", ct);

            // GetDomainRecordsAsync: records -> expiry
            await EnsureIndexAsync(conn, $"DX_BigMapKeys_JsonKey:{registry.ExpiryBigMap}", $"""
                ON "BigMapKeys" ("JsonKey")
                WHERE "BigMapId" = {registry.ExpiryBigMap}
                """, ct);

            // GetDomainRecordsAsync: records -> reverse
            await EnsureIndexAsync(conn, $"DX_BigMapKeys_JsonKey:{registry.ReverseBigMap}", $"""
                ON "BigMapKeys" ("JsonKey")
                WHERE "BigMapId" = {registry.ReverseBigMap}
                """, ct);

            // UpdateExpirationsAsync: expiry -> records
            await EnsureIndexAsync(conn, $"DX_BigMapKeys_ExpiryKey:{registry.RecordsBigMap}", $"""
                ON "BigMapKeys" (("JsonValue" -> 'expiry_key'))
                WHERE "BigMapId" = {registry.RecordsBigMap}
                """, ct);

            // UpdateReverseRecordsAsync: reverse -> domains
            await EnsureIndexAsync(conn, "DX_Domains_RegistryId_Address", """
                ON "Domains" ("RegistryId", "Address")
                """, ct);

            // GetInvalidatedDomainRecordsAsync, DeleteReorgedDomainsAsync: domains touched by reorged levels
            await EnsureIndexAsync(conn, "DX_Domains_RegistryId_LastLevel", """
                ON "Domains" ("RegistryId", "LastLevel")
                """, ct);
        }
        finally
        {
            _sema.Release();
        }
    }

    static async Task EnsureIndexAsync(NpgsqlConnection conn, string name, string definition, CancellationToken ct)
    {
        var state = await GetIndexStateAsync(conn, name, ct);
        if (state == IndexState.Valid)
            return;

        if (state == IndexState.Abandoned)
            await ExecuteAsync(conn, $"""
                DROP INDEX CONCURRENTLY IF EXISTS "{name}"
                """, ct);

        await ExecuteAsync(conn, $"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS "{name}"
            {definition}
            """, ct);
    }

    static async Task<IndexState> GetIndexStateAsync(NpgsqlConnection conn, string name, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT i.indisvalid,
                   EXISTS (
                       SELECT 1
                       FROM pg_stat_progress_create_index AS p
                       WHERE p.index_relid = i.indexrelid
                   )
            FROM pg_index AS i
            INNER JOIN pg_class AS c ON c.oid = i.indexrelid
            INNER JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE n.nspname = current_schema()
            AND c.relname = @name
            """, conn);
        cmd.Parameters.AddWithValue("name", name);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return IndexState.Missing;
        if (reader.GetBoolean(0)) return IndexState.Valid;
        return reader.GetBoolean(1) ? IndexState.Building : IndexState.Abandoned;
    }

    enum IndexState
    {
        Missing,
        Valid,
        Building,
        Abandoned
    }
    #endregion

    #region domains
    public async Task<int?> GetBatchLastLevelAsync(NpgsqlConnection conn, RegistryInfo registry, int from, int to, int limit, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT MAX(level)
            FROM (
                SELECT "LastLevel" AS level
                FROM "BigMapKeys"
                WHERE "ChainId" = @chain
                AND "BigMapId" = @records
                AND "LastLevel" >= @from
                AND "LastLevel" <= @to
                ORDER BY "LastLevel"
                LIMIT @limit
            ) AS batch
            """, conn);
        cmd.Parameters.AddWithValue("chain", registry.ChainId);
        cmd.Parameters.AddWithValue("records", registry.RecordsBigMap);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("limit", limit);

        return await cmd.ExecuteScalarAsync(ct) as int?;
    }

    public async Task<List<DomainRecord>> GetDomainRecordsAsync(NpgsqlConnection conn, RegistryInfo registry, int from, int to, int tail, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(GetDomainRecordsSql(registry, """
            record."ChainId" = @chain
            AND (record."LastLevel" BETWEEN @from AND @to OR record."LastLevel" > @tail)
            """), conn);
        cmd.Parameters.AddWithValue("chain", registry.ChainId);
        cmd.Parameters.AddWithValue("records", registry.RecordsBigMap);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("tail", tail);
        cmd.Parameters.AddWithValue("maxDateTime", MaxDateTime);

        return await ReadDomainRecordsAsync(cmd, ct);
    }

    public async Task<List<DomainRecord>> GetInvalidatedDomainRecordsAsync(NpgsqlConnection conn, RegistryInfo registry, int from, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand(GetDomainRecordsSql(registry, """
            record."Id" IN (
                SELECT "Id"
                FROM "Domains"
                WHERE "RegistryId" = @registry
                AND "LastLevel" >= @from
            )
            """), conn);
        cmd.Parameters.AddWithValue("records", registry.RecordsBigMap);
        cmd.Parameters.AddWithValue("registry", registry.Id);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("maxDateTime", MaxDateTime);

        return await ReadDomainRecordsAsync(cmd, ct);
    }

    public async Task<int> DeleteReorgedDomainsAsync(NpgsqlConnection conn, RegistryInfo registry, int from, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand("""
            DELETE FROM "Domains" AS domain
            WHERE domain."RegistryId" = @registry
            AND domain."LastLevel" >= @from
            AND NOT EXISTS (
                SELECT 1
                FROM "BigMapKeys" AS record
                WHERE record."Id" = domain."Id"
                AND record."BigMapId" = @records
            )
            """, conn);
        cmd.Parameters.AddWithValue("registry", registry.Id);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("records", registry.RecordsBigMap);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    static string GetDomainRecordsSql(RegistryInfo registry, string filter)
    {
        // the joined big map ids stay literals for partial indexes
        return $$"""
            SELECT record."Id",
                   record."FirstLevel",
                   record."FirstTimestamp",
                   record."LastLevel",
                   GREATEST(record."LastLevel", expiry."LastLevel", reverse."LastLevel"),
                   GREATEST(record."LastTimestamp", expiry."LastTimestamp", reverse."LastTimestamp"),
                   record."JsonKey" #>> '{}',
                   record."JsonValue" ->> 'level',
                   record."JsonValue" ->> 'owner',
                   record."JsonValue" ->> 'address',
                   record."JsonValue" -> 'data',
                   {{ExpirationSql}},
                   COALESCE(reverse."Active" AND reverse."JsonValue" -> 'name' = record."JsonKey", false)
            FROM "BigMapKeys" AS record
            LEFT JOIN "BigMapKeys" AS expiry
            ON expiry."BigMapId" = {{registry.ExpiryBigMap}}
            AND expiry."JsonKey" = record."JsonValue" -> 'expiry_key'
            LEFT JOIN "BigMapKeys" AS reverse
            ON reverse."BigMapId" = {{registry.ReverseBigMap}}
            AND reverse."JsonKey" = record."JsonValue" -> 'address'
            WHERE record."BigMapId" = @records
            AND {{filter}}
            """;
    }

    static async Task<List<DomainRecord>> ReadDomainRecordsAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var res = new List<DomainRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            res.Add(new DomainRecord(
                Id: reader.GetInt64(0),
                FirstLevel: reader.GetInt32(1),
                FirstTimestamp: reader.GetDateTime(2),
                LastLevel: reader.GetInt32(3),
                MaxLastLevel: reader.GetInt32(4),
                MaxLastTimestamp: reader.GetDateTime(5),
                NameHex: GetString(reader, 6),
                Level: GetString(reader, 7),
                Owner: GetString(reader, 8),
                Address: GetString(reader, 9),
                Data: GetString(reader, 10),
                Expiration: reader.GetDateTime(11),
                Reverse: reader.GetBoolean(12)));
        }

        return res;
    }

    public async Task<int> SaveDomainsAsync(NpgsqlConnection conn, int chainId, int registryId, List<DomainInfo> domains, CancellationToken ct = default)
    {
        if (domains.Count == 0) return 0;

        var ids = new long[domains.Count];
        var levels = new int[domains.Count];
        var names = new string[domains.Count];
        var owners = new string[domains.Count];
        var addresses = new string?[domains.Count];
        var reverses = new bool[domains.Count];
        var expirations = new DateTime[domains.Count];
        var datas = new string?[domains.Count];
        var firstLevels = new int[domains.Count];
        var firstTimestamps = new DateTime[domains.Count];
        var lastLevels = new int[domains.Count];
        var lastTimestamps = new DateTime[domains.Count];

        var i = 0;
        foreach (var d in domains)
        {
            ids[i] = d.Id;
            levels[i] = d.Level;
            names[i] = d.Name;
            owners[i] = d.Owner;
            addresses[i] = d.Address;
            reverses[i] = d.Reverse;
            expirations[i] = d.Expiration;
            datas[i] = d.Data;
            firstLevels[i] = d.FirstLevel;
            firstTimestamps[i] = d.FirstTimestamp;
            lastLevels[i] = d.LastLevel;
            lastTimestamps[i] = d.LastTimestamp;
            i++;
        }

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO "Domains" ("Id", "ChainId", "RegistryId", "Level", "Name", "Owner", "Address", "Reverse", "Expiration", "Data", "FirstLevel", "FirstTimestamp", "LastLevel", "LastTimestamp")
            SELECT v.id, @chainId, @registryId, v.level, v.name, v.owner, v.address, v.reverse, v.expiration, v.data::jsonb, v.firstLevel, v.firstTimestamp, v.lastLevel, v.lastTimestamp
            FROM unnest(@ids, @levels, @names, @owners, @addresses, @reverses, @expirations, @datas, @firstLevels, @firstTimestamps, @lastLevels, @lastTimestamps)
                AS v(id, level, name, owner, address, reverse, expiration, data, firstLevel, firstTimestamp, lastLevel, lastTimestamp)
            ON CONFLICT ("Id") DO UPDATE SET
                "ChainId" = EXCLUDED."ChainId",
                "RegistryId" = EXCLUDED."RegistryId",
                "Level" = EXCLUDED."Level",
                "Name" = EXCLUDED."Name",
                "Owner" = EXCLUDED."Owner",
                "Address" = EXCLUDED."Address",
                "Reverse" = EXCLUDED."Reverse",
                "Expiration" = EXCLUDED."Expiration",
                "Data" = EXCLUDED."Data",
                "FirstLevel" = EXCLUDED."FirstLevel",
                "FirstTimestamp" = EXCLUDED."FirstTimestamp",
                "LastLevel" = EXCLUDED."LastLevel",
                "LastTimestamp" = EXCLUDED."LastTimestamp"
            """, conn);
        cmd.Parameters.AddWithValue("chainId", chainId);
        cmd.Parameters.AddWithValue("registryId", registryId);
        cmd.Parameters.AddWithValue("ids", ids);
        cmd.Parameters.AddWithValue("levels", levels);
        cmd.Parameters.AddWithValue("names", names);
        cmd.Parameters.AddWithValue("owners", owners);
        cmd.Parameters.AddWithValue("addresses", addresses);
        cmd.Parameters.AddWithValue("reverses", reverses);
        cmd.Parameters.AddWithValue("expirations", expirations);
        cmd.Parameters.AddWithValue("datas", datas);
        cmd.Parameters.AddWithValue("firstLevels", firstLevels);
        cmd.Parameters.AddWithValue("firstTimestamps", firstTimestamps);
        cmd.Parameters.AddWithValue("lastLevels", lastLevels);
        cmd.Parameters.AddWithValue("lastTimestamps", lastTimestamps);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> UpdateExpirationsAsync(NpgsqlConnection conn, RegistryInfo registry, int from, int to, int tail, CancellationToken ct = default)
    {
        // the joined big map id stays literal for partial indexes
        await using var cmd = new NpgsqlCommand($$"""
            UPDATE "Domains" AS target
            SET "Expiration" = updates.expiration,
                "LastLevel" = GREATEST(target."LastLevel", updates.level),
                "LastTimestamp" = GREATEST(target."LastTimestamp", updates.timestamp)
            FROM (
                SELECT domain."Id" AS id,
                       expiry.expiration AS expiration,
                       expiry.level AS level,
                       expiry.timestamp AS timestamp
                FROM (
                    SELECT record."Id" AS id,
                           {{ExpirationSql}} AS expiration,
                           GREATEST(expiry."LastLevel", record."LastLevel") AS level,
                           GREATEST(expiry."LastTimestamp", record."LastTimestamp") AS timestamp
                    FROM "BigMapKeys" AS expiry
                    INNER JOIN "BigMapKeys" AS record
                    ON record."BigMapId" = {{registry.RecordsBigMap}}
                    AND record."JsonValue" -> 'expiry_key' = expiry."JsonKey"
                    WHERE expiry."ChainId" = @chain
                    AND expiry."BigMapId" = @expiry
                    AND (expiry."LastLevel" BETWEEN @from AND @to OR expiry."LastLevel" > @tail)
                ) AS expiry
                INNER JOIN "Domains" AS domain
                ON domain."Id" = expiry.id
                WHERE (domain."Expiration" != expiry.expiration OR domain."LastLevel" < expiry.level)
                FOR UPDATE OF domain
            ) AS updates
            WHERE target."Id" = updates.id
            """, conn);
        cmd.Parameters.AddWithValue("chain", registry.ChainId);
        cmd.Parameters.AddWithValue("expiry", registry.ExpiryBigMap);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("tail", tail);
        cmd.Parameters.AddWithValue("maxDateTime", MaxDateTime);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> UpdateReverseRecordsAsync(NpgsqlConnection conn, RegistryInfo registry, int from, int to, int tail, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand("""
            UPDATE "Domains" AS target
            SET "Reverse" = updates.reverse,
                "LastLevel" = GREATEST(target."LastLevel", updates.level),
                "LastTimestamp" = GREATEST(target."LastTimestamp", updates.timestamp)
            FROM (
                SELECT domain."Id" AS id,
                       COALESCE(encode(convert_to(domain."Name", 'UTF8'), 'hex') = reverse.name, false) AS reverse,
                       reverse.level AS level,
                       reverse.timestamp AS timestamp
                FROM (
                    SELECT reverse."JsonKey" #>> '{}' AS address,
                           CASE WHEN reverse."Active" THEN reverse."JsonValue" ->> 'name' END AS name,
                           reverse."LastLevel" AS level,
                           reverse."LastTimestamp" AS timestamp
                    FROM "BigMapKeys" AS reverse
                    WHERE reverse."ChainId" = @chain
                    AND reverse."BigMapId" = @reverse
                    AND (reverse."LastLevel" BETWEEN @from AND @to OR reverse."LastLevel" > @tail)
                ) AS reverse
                INNER JOIN "Domains" AS domain
                ON domain."RegistryId" = @registry
                AND domain."Address" = reverse.address
                WHERE (COALESCE(encode(convert_to(domain."Name", 'UTF8'), 'hex') = reverse.name, false) != domain."Reverse"
                    OR domain."LastLevel" < reverse.level)
                FOR UPDATE OF domain
            ) AS updates
            WHERE target."Id" = updates.id
            """, conn);
        cmd.Parameters.AddWithValue("chain", registry.ChainId);
        cmd.Parameters.AddWithValue("reverse", registry.ReverseBigMap);
        cmd.Parameters.AddWithValue("registry", registry.Id);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("tail", tail);

        return await cmd.ExecuteNonQueryAsync(ct);
    }
    #endregion

    #region state
    public async Task<string?> GetStateAsync(int chainId, string registry, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT ("Extras" #> ARRAY['domains', 'tezosDomains', @registry])::text
            FROM "Chains"
            WHERE "Id" = @chain
            LIMIT 1
            """);
        cmd.Parameters.AddWithValue("chain", chainId);
        cmd.Parameters.AddWithValue("registry", registry);

        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    public async Task<int> SaveStateAsync(int chainId, string registry, string json, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            UPDATE "Chains"
            SET "Extras" = jsonb_set(
                jsonb_set(
                    jsonb_set(
                        COALESCE("Extras", '{}'),
                        '{domains}',
                        COALESCE("Extras" -> 'domains', '{}')
                    ),
                    '{domains,tezosDomains}',
                    COALESCE("Extras" #> '{domains,tezosDomains}', '{}')
                ),
                ARRAY['domains', 'tezosDomains', @registry],
                @state::jsonb
            )
            WHERE "Id" = @chain
            """);
        cmd.Parameters.AddWithValue("chain", chainId);
        cmd.Parameters.AddWithValue("registry", registry);
        cmd.Parameters.AddWithValue("state", json);

        return await cmd.ExecuteNonQueryAsync(ct);
    }
    #endregion

    static async Task ExecuteAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 0 };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    static string? GetString(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
