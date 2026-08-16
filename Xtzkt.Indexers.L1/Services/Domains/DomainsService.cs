using Dapper;
using Dynamic.Json;
using Netezos.Encoding;
using Npgsql;
using System.Text;
using System.Text.Json;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.L1.Services.Domains
{
    public class DomainsService(IConfiguration config, ILogger<DomainsService> logger) : BackgroundService
    {
        readonly string ConnectionString = config.GetDbConnectionString();
        readonly DomainsConfig Config = config.GetDomainsConfig();
        readonly ChainConfig Chain = config.GetChainConfig();
        readonly ILogger Logger = logger;

        #region state
        int RecordsBigMap = -1;
        int ReverseBigMap = -1;
        int ExpiryBigMap = -1;
        int Level = -1;

        #endregion

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                Logger.LogInformation("Domains plugin started");

                await InitState();
                if (RecordsBigMap == -1 || ExpiryBigMap == -1 || ReverseBigMap == -1)
                {
                    Logger.LogWarning("Domains plugin configured with an invalid name registry contract");
                    return;
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await SyncDomains(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to sync domains");
                    }
                    await Task.Delay(Config.PeriodSec * 1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Domains plugin crashed");
            }
            finally
            {
                Logger.LogInformation("Domains plugin stopped");
            }
        }

        async Task SyncDomains(CancellationToken ct)
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            while (!ct.IsCancellationRequested)
            {
                var (pending, lastLevel) = await GetPendingDomains(conn, 10_000);
                if (pending.Count == 0)
                {
                    await UpdateExpirations(conn);
                    await UpdateReverseRecords(conn);
                    break;
                }

                await SaveDomains(conn, pending);
                await UpdateExpirations(conn);
                await UpdateReverseRecords(conn);

                if (pending.Count == 10_000)
                {
                    await SaveState(conn, lastLevel);
                    Level = lastLevel;
                }
                else
                {
                    await SaveState(conn, lastLevel + 1);
                    Level = lastLevel + 1;
                    break;
                }
            }
        }

        async Task<(List<Domain>, int)> GetPendingDomains(NpgsqlConnection conn, int limit)
        {
            var rows = await conn.QueryAsync("""
                SELECT  record."Id",
                        record."FirstLevel",
                        record."LastLevel",
                        GREATEST(record."LastLevel", expiry."LastLevel", reverse."LastLevel") as "MaxLastLevel",
                        convert_from(decode(record."JsonKey"#>>'{}', 'hex'), 'utf-8') as "Name",
                        record."JsonValue"->>'level' as "Level",
                        record."JsonValue"->>'address' as "Address",
                        record."JsonValue"->>'owner' as "Owner",
                        record."JsonValue"->'data' as "Data",
                        expiry."JsonValue"#>>'{}' as "Expiration",
                        COALESCE(reverse."JsonValue"->'name' = record."JsonKey", false) as "Reverse",
                        chain."Level" as "State"
                FROM "BigMapKeys" as record
                LEFT JOIN "BigMapKeys" as expiry
                ON expiry."BigMapId" = @id2
                AND expiry."JsonKey" = record."JsonValue"->'expiry_key'
                LEFT JOIN "BigMapKeys" as reverse
                ON reverse."BigMapId" = @id3
                AND reverse."JsonKey" = record."JsonValue"->'address'
                INNER JOIN "Chains" as chain
                ON chain."Id" = @chain
                WHERE record."ChainId" = @chain
                AND record."BigMapId" = @id
                AND record."LastLevel" >= @level
                AND record."LastLevel" < chain."Level"
                ORDER BY record."LastLevel"
                LIMIT @limit
                """, new { id = RecordsBigMap, id2 = ExpiryBigMap, id3 = ReverseBigMap, level = Level, chain = Chain.Id, limit });

            var res = new List<Domain>(rows.Count());
            foreach (var row in rows)
            {
                try
                {
                    if (row.MaxLastLevel >= row.State)
                    {
                        // avoid reorgs
                        Logger.LogWarning("Unconfirmed domain update postponed");
                        return (res, row.LastLevel - 1);
                    }

                    res.Add(new Domain
                    {
                        Id = row.Id,
                        ChainId = Chain.Id,
                        Level = int.Parse(row.Level),
                        Name = row.Name,
                        Owner = row.Owner,
                        Address = row.Address,
                        Reverse = row.Reverse,
                        Expiration = row.Expiration is string s 
                            ? DateTimeOffset.Parse(s).UtcDateTime
                            : DateTimeOffset.MaxValue.UtcDateTime.Date,
                        Data = row.Data == "{}" ? null : ParseDomainData((string)row.Data),
                        FirstLevel = row.FirstLevel,
                        LastLevel = row.MaxLastLevel
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to parse domain record {id}", (int)row.Id);
                }
            }
            return (res, rows.LastOrDefault()?.LastLevel ?? 0);
        }

        static async Task SaveDomains(NpgsqlConnection conn, List<Domain> domains)
        {
            for (int i = 0; i < domains.Count; i += 1000)
            {
                var p = 0;
                var sql = new StringBuilder();
                var param = new DynamicParameters();
                var max = Math.Min(1000, domains.Count - i);

                sql.AppendLine(@"INSERT INTO ""Domains"" (""Id"", ""ChainId"", ""Level"", ""Name"", ""Owner"", ""Address"", ""Reverse"", ""Expiration"", ""Data"", ""FirstLevel"", ""LastLevel"") VALUES");
                for (int j = 0; j < max; j++)
                {
                    var domain = domains[i + j];
                    param.Add($"@p{p}", domain.Id);
                    param.Add($"@p{p + 1}", domain.ChainId);
                    param.Add($"@p{p + 2}", domain.Level);
                    param.Add($"@p{p + 3}", domain.Name);
                    param.Add($"@p{p + 4}", domain.Owner);
                    param.Add($"@p{p + 5}", domain.Address);
                    param.Add($"@p{p + 6}", domain.Reverse);
                    param.Add($"@p{p + 7}", domain.Expiration);
                    param.Add($"@p{p + 8}", domain.Data == null ? null : JsonSerializer.Serialize(domain.Data));
                    param.Add($"@p{p + 9}", domain.FirstLevel);
                    param.Add($"@p{p + 10}", domain.LastLevel);
                    sql.Append($"(@p{p}, @p{p + 1}, @p{p + 2}, @p{p + 3}, @p{p + 4}, @p{p + 5}, @p{p + 6}, @p{p + 7}::jsonb, @p{p + 8}, @p{p + 9}, @p{p + 10})");
                    if (j < max - 1) sql.Append(',');
                    sql.AppendLine();
                    p += 11;
                }
                sql.AppendLine(@"ON CONFLICT (""Id"") DO UPDATE SET");
                sql.AppendLine(@"""ChainId"" = EXCLUDED.""ChainId"",");
                sql.AppendLine(@"""Level"" = EXCLUDED.""Level"",");
                sql.AppendLine(@"""Name"" = EXCLUDED.""Name"",");
                sql.AppendLine(@"""Owner"" = EXCLUDED.""Owner"",");
                sql.AppendLine(@"""Address"" = EXCLUDED.""Address"",");
                sql.AppendLine(@"""Reverse"" = EXCLUDED.""Reverse"",");
                sql.AppendLine(@"""Expiration"" = EXCLUDED.""Expiration"",");
                sql.AppendLine(@"""Data"" = EXCLUDED.""Data"",");
                sql.AppendLine(@"""FirstLevel"" = EXCLUDED.""FirstLevel"",");
                sql.AppendLine(@"""LastLevel"" = EXCLUDED.""LastLevel"";");

                await conn.ExecuteAsync(sql.ToString(), param);
            }
        }

        async Task UpdateExpirations(NpgsqlConnection conn)
        {
            await conn.ExecuteAsync("""
                UPDATE "Domains" 
                SET "Expiration" = updates.expiration,
                    "LastLevel" = GREATEST("LastLevel", level)
                FROM (
                	SELECT id, expiration, level
                	FROM (
                		SELECT  record."Id" AS id,
                				(expiry."JsonValue"#>>'{}')::timestamptz AS expiration,
                                expiry."LastLevel" as level
                		FROM "BigMapKeys" AS expiry
                		INNER JOIN "BigMapKeys" AS record
                		ON record."BigMapId" = @id
                		AND record."JsonValue"->'expiry_key' = expiry."JsonKey"
                		INNER JOIN "Chains" AS chain
                		ON chain."Id" = @chain
                		WHERE expiry."ChainId" = @chain
                        AND expiry."BigMapId" = @id2
                		AND expiry."LastLevel" >= @level
                		AND expiry."LastLevel" < chain."Level"
                	) expiry_map
                	INNER JOIN "Domains" AS domain
                	ON domain."Id" = id
                	WHERE domain."Expiration" != expiration
                    FOR UPDATE
                ) updates
                WHERE "Id" = updates.id
                """, new { id = RecordsBigMap, id2 = ExpiryBigMap, level = Level, chain = Chain.Id });
        }

        async Task UpdateReverseRecords(NpgsqlConnection conn)
        {
            await conn.ExecuteAsync("""
                UPDATE "Domains" 
                SET "Reverse" = updates.reverse,
                    "LastLevel" = GREATEST("LastLevel", level)
                FROM (
                	SELECT 	domain."Id" as id,
                			COALESCE(domain."Name" = name, false) as reverse,
                            level
                	FROM (
                		SELECT 	revers."JsonKey"#>>'{}' as address,
                				convert_from(decode(revers."JsonValue"->>'name', 'hex'), 'utf-8') as name,
                                revers."LastLevel" as level
                		FROM "BigMapKeys" AS revers
                		INNER JOIN "Chains" AS chain
                		ON chain."Id" = @chain
                		WHERE revers."ChainId" = @chain
                        AND revers."BigMapId" = @id3
                		AND revers."LastLevel" >= @level
                		AND revers."LastLevel" < chain."Level"
                	) reverse_records
                	INNER JOIN "Domains" AS domain
                	ON domain."ChainId" = @chain AND domain."Address" = address
                	WHERE COALESCE(domain."Name" = name, false) != domain."Reverse"
                	FOR UPDATE
                ) updates
                WHERE "Id" = updates.id
                """, new { id3 = ReverseBigMap, level = Level, chain = Chain.Id });
        }

        async Task InitState()
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            var (contract, level) = await GetState(conn);
            if (contract != Config.NameRegistry)
            {
                await conn.ExecuteAsync(@"DELETE FROM ""Domains"" WHERE ""ChainId"" = {0}", Chain.Id);
                await SaveState(conn, Config.NameRegistry);
                (contract, level) = (Config.NameRegistry, 0);
            }

            var contractId = await conn.QueryFirstOrDefaultAsync<int>("""
                SELECT "Id"
                FROM "Addresses"
                WHERE "ChainId" = @chain
                AND "Hash" = @contract
                LIMIT 1
                """, new { chain = Chain.Id, contract });

            if (contractId == 0) return;

            var recordsBigMap = await conn.QueryFirstOrDefaultAsync<int>("""
                SELECT "Id"
                FROM "BigMaps"
                WHERE "ContractId" = @contractId
                AND "StoragePath" = 'store.records'
                AND "Active" = true
                LIMIT 1
                """, new { contractId });

            if (recordsBigMap == 0) return;

            var reverseBigMap = await conn.QueryFirstOrDefaultAsync<int>("""
                SELECT "Id"
                FROM "BigMaps"
                WHERE "ContractId" = @contractId
                AND "StoragePath" = 'store.reverse_records'
                AND "Active" = true
                LIMIT 1
                """, new { contractId });

            if (reverseBigMap == 0) return;

            var expiryBigMap = await conn.QueryFirstOrDefaultAsync<int>("""
                SELECT "Id"
                FROM "BigMaps"
                WHERE "ContractId" = @contractId
                AND "StoragePath" = 'store.expiry_map'
                AND "Active" = true
                LIMIT 1
                """, new { contractId });

            if (expiryBigMap == 0) return;

            RecordsBigMap = recordsBigMap;
            ReverseBigMap = reverseBigMap;
            ExpiryBigMap = expiryBigMap;
            Level = level;
        }

        async Task<(string, int)> GetState(NpgsqlConnection conn)
        {
            var row = await conn.QueryFirstAsync("""
                SELECT "DomainsNameRegistry", "DomainsLevel"
                FROM "Chains"
                WHERE "Id" = @id
                LIMIT 1
                """, new { id = Chain.Id });
            return (row.DomainsNameRegistry, row.DomainsLevel);
        }

        async Task SaveState(NpgsqlConnection conn, string contract)
        {
            await conn.ExecuteAsync("""
                UPDATE "Chains"
                SET "DomainsNameRegistry" = @contract, "DomainsLevel" = 0
                WHERE "Id" = @id
                """, new { contract, id = Chain.Id });
        }

        async Task SaveState(NpgsqlConnection conn, int level)
        {
            await conn.ExecuteAsync("""
                UPDATE "Chains"
                SET "DomainsLevel" = @level
                WHERE "Id" = @id
                """, new { level, id = Chain.Id });
        }

        static JsonElement ParseDomainData(string data)
        {
            var res = new Dictionary<string, object>();
            foreach (var prop in DJson.Parse(data))
            {
                var bytes = Hex.Parse((string)prop.Value);
                try
                {
                    res[(string)prop.Name] = JsonSerializer.Deserialize<JsonElement>(bytes);
                }
                catch
                {
                    res[(string)prop.Name] = IsReadable(bytes) ? Utf8.Convert(bytes) : (string)prop.Value;
                }
            }
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(res));
        }

        static bool IsReadable(byte[] bytes) => bytes.Count(x => x >= 32 && x <= 126) / (double)bytes.Length > 0.8;
    }
}
