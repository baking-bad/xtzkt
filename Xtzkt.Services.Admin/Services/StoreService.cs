using System.Data;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Services.Admin.Exceptions;

namespace Xtzkt.Services.Admin.Services;

class StoreService(NpgsqlDataSource _dataSource, ILogger<StoreService> _logger)
{
    public async Task<(long Count, DateTime? LastUpdate)> GetStateAsync(CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand("""
            SELECT COUNT(*), MAX("UpdatedAt")
            FROM "Profiles"
            """);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return (reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1));
    }

    public async Task<List<Profile>> GetProfilesAsync(ProfileType? type, int offset, int limit, CancellationToken ct = default)
    {
        var filter = type == null ? "" : """WHERE "Type" = @type""";

        await using var cmd = _dataSource.CreateCommand($"""
            SELECT *
            FROM "Profiles"
            {filter}
            ORDER BY "UpdatedAt", "Id"
            OFFSET @offset
            LIMIT @limit
            """);
        if (type != null) cmd.Parameters.AddWithValue("type", (int)type.Value);
        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddWithValue("limit", limit);

        var res = new List<Profile>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString("Id");
            var name = reader.GetString("Name");
            var updatedAt = reader.GetDateTime("UpdatedAt");

            res.Add((ProfileType)reader.GetInt32("Type") switch
            {
                ProfileType.Address => new AddressProfile
                {
                    Id = id,
                    Name = name,
                    UpdatedAt = updatedAt,
                    Description = GetString(reader, "Description"),
                    Logo = GetString(reader, "Logo"),
                    LogoDark = GetString(reader, "LogoDark"),
                    Website = GetString(reader, "Website"),
                    Support = GetString(reader, "Support"),
                    Email = GetString(reader, "Email"),
                    Telegram = GetString(reader, "Telegram"),
                    Discord = GetString(reader, "Discord"),
                    Reddit = GetString(reader, "Reddit"),
                    Slack = GetString(reader, "Slack"),
                    Github = GetString(reader, "Github"),
                    Gitlab = GetString(reader, "Gitlab"),
                    Mailchain = GetString(reader, "Mailchain"),
                    Instagram = GetString(reader, "Instagram"),
                    Facebook = GetString(reader, "Facebook"),
                    X = GetString(reader, "X")
                },
                ProfileType.Protocol => new ProtocolProfile
                {
                    Id = id,
                    Name = name,
                    UpdatedAt = updatedAt,
                    Docs = GetString(reader, "Docs")
                },
                ProfileType.Software => new SoftwareProfile
                {
                    Id = id,
                    Name = name,
                    UpdatedAt = updatedAt,
                    CommitDate = reader.IsDBNull("CommitDate") ? null : reader.GetDateTime("CommitDate"),
                    CommitHash = GetString(reader, "CommitHash")
                },
                var unknown => throw new NotSupportedException($"Unsupported profile type {unknown}")
            });
        }

        return res;
    }

    public async Task<(int Upserted, int Removed)> UpdateProfilesAsync(List<Profile> upserts, List<string> removes, CancellationToken ct = default)
    {
        if (upserts.Count == 0 && removes.Count == 0) return (0, 0);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var upserted = await UpsertAsync(conn, upserts, ct);
        var removed = await RemoveAsync(conn, removes, ct);

        await tx.CommitAsync(ct);

        _logger.LogInformation("Profiles updated: {upserted} upserted, {removed} removed", upserted, removed);
        return (upserted, removed);
    }

    static async Task<int> RemoveAsync(NpgsqlConnection conn, List<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return 0;

        await using var cmd = new NpgsqlCommand("""
            DELETE FROM "Profiles"
            WHERE "Id" = ANY(@ids)
            """, conn);
        cmd.Parameters.AddWithValue("ids", ids);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    static async Task<int> UpsertAsync(NpgsqlConnection conn, List<Profile> profiles, CancellationToken ct)
    {
        if (profiles.Count == 0) return 0;

        var ids = new string[profiles.Count];
        var types = new int[profiles.Count];
        var names = new string[profiles.Count];
        var updatedAts = new DateTime[profiles.Count];
        var descriptions = new string?[profiles.Count];
        var logos = new string?[profiles.Count];
        var logoDarks = new string?[profiles.Count];
        var websites = new string?[profiles.Count];
        var supports = new string?[profiles.Count];
        var emails = new string?[profiles.Count];
        var telegrams = new string?[profiles.Count];
        var discords = new string?[profiles.Count];
        var reddits = new string?[profiles.Count];
        var slacks = new string?[profiles.Count];
        var githubs = new string?[profiles.Count];
        var gitlabs = new string?[profiles.Count];
        var mailchains = new string?[profiles.Count];
        var instagrams = new string?[profiles.Count];
        var facebooks = new string?[profiles.Count];
        var xs = new string?[profiles.Count];
        var docs = new string?[profiles.Count];
        var commitDates = new DateTime?[profiles.Count];
        var commitHashes = new string?[profiles.Count];

        var i = 0;
        foreach (var p in profiles)
        {
            ids[i] = p.Id;
            types[i] = (int)p.Type;
            names[i] = p.Name;
            updatedAts[i] = p.UpdatedAt;
            switch (p)
            {
                case AddressProfile address:
                    descriptions[i] = address.Description;
                    logos[i] = address.Logo;
                    logoDarks[i] = address.LogoDark;
                    websites[i] = address.Website;
                    supports[i] = address.Support;
                    emails[i] = address.Email;
                    telegrams[i] = address.Telegram;
                    discords[i] = address.Discord;
                    reddits[i] = address.Reddit;
                    slacks[i] = address.Slack;
                    githubs[i] = address.Github;
                    gitlabs[i] = address.Gitlab;
                    mailchains[i] = address.Mailchain;
                    instagrams[i] = address.Instagram;
                    facebooks[i] = address.Facebook;
                    xs[i] = address.X;
                    break;
                case ProtocolProfile protocol:
                    docs[i] = protocol.Docs;
                    break;
                case SoftwareProfile software:
                    commitDates[i] = software.CommitDate;
                    commitHashes[i] = software.CommitHash;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported profile type {p.GetType().Name}");
            }
            i++;
        }

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO "Profiles" ("Id", "Type", "Name", "UpdatedAt", "Description", "Logo", "LogoDark", "Website", "Support", "Email", "Telegram", "Discord", "Reddit", "Slack", "Github", "Gitlab", "Mailchain", "Instagram", "Facebook", "X", "Docs", "CommitDate", "CommitHash")
            SELECT *
            FROM unnest(@ids, @types, @names, @updatedAts, @descriptions, @logos, @logoDarks, @websites, @supports, @emails, @telegrams, @discords, @reddits, @slacks, @githubs, @gitlabs, @mailchains, @instagrams, @facebooks, @xs, @docs, @commitDates, @commitHashes)
            ON CONFLICT ("Id") DO UPDATE SET
                "Name" = EXCLUDED."Name",
                "UpdatedAt" = EXCLUDED."UpdatedAt",
                "Description" = EXCLUDED."Description",
                "Logo" = EXCLUDED."Logo",
                "LogoDark" = EXCLUDED."LogoDark",
                "Website" = EXCLUDED."Website",
                "Support" = EXCLUDED."Support",
                "Email" = EXCLUDED."Email",
                "Telegram" = EXCLUDED."Telegram",
                "Discord" = EXCLUDED."Discord",
                "Reddit" = EXCLUDED."Reddit",
                "Slack" = EXCLUDED."Slack",
                "Github" = EXCLUDED."Github",
                "Gitlab" = EXCLUDED."Gitlab",
                "Mailchain" = EXCLUDED."Mailchain",
                "Instagram" = EXCLUDED."Instagram",
                "Facebook" = EXCLUDED."Facebook",
                "X" = EXCLUDED."X",
                "Docs" = EXCLUDED."Docs",
                "CommitDate" = EXCLUDED."CommitDate",
                "CommitHash" = EXCLUDED."CommitHash"
            WHERE "Profiles"."Type" = EXCLUDED."Type"
            RETURNING "Id"
            """, conn);
        cmd.Parameters.AddWithValue("ids", ids);
        cmd.Parameters.AddWithValue("types", types);
        cmd.Parameters.AddWithValue("names", names);
        cmd.Parameters.AddWithValue("updatedAts", updatedAts);
        cmd.Parameters.AddWithValue("descriptions", descriptions);
        cmd.Parameters.AddWithValue("logos", logos);
        cmd.Parameters.AddWithValue("logoDarks", logoDarks);
        cmd.Parameters.AddWithValue("websites", websites);
        cmd.Parameters.AddWithValue("supports", supports);
        cmd.Parameters.AddWithValue("emails", emails);
        cmd.Parameters.AddWithValue("telegrams", telegrams);
        cmd.Parameters.AddWithValue("discords", discords);
        cmd.Parameters.AddWithValue("reddits", reddits);
        cmd.Parameters.AddWithValue("slacks", slacks);
        cmd.Parameters.AddWithValue("githubs", githubs);
        cmd.Parameters.AddWithValue("gitlabs", gitlabs);
        cmd.Parameters.AddWithValue("mailchains", mailchains);
        cmd.Parameters.AddWithValue("instagrams", instagrams);
        cmd.Parameters.AddWithValue("facebooks", facebooks);
        cmd.Parameters.AddWithValue("xs", xs);
        cmd.Parameters.AddWithValue("docs", docs);
        cmd.Parameters.AddWithValue("commitDates", commitDates);
        cmd.Parameters.AddWithValue("commitHashes", commitHashes);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var saved = new HashSet<string>(profiles.Count);
        while (await reader.ReadAsync(ct))
            saved.Add(reader.GetString(0));

        if (saved.Count != ids.Length)
            throw new ProfileTypeChangeException(ids.First(x => !saved.Contains(x)));

        return saved.Count;
    }

    #region indexes
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await ExecuteAsync(conn, "SET statement_timeout = 0", ct);

        // GetProfilesAsync: filtered by type
        await EnsureIndexAsync(conn, "PX_Profiles_Type_UpdatedAt_Id", """
            ON "Profiles" ("Type", "UpdatedAt", "Id")
            """, ct);

        // GetProfilesAsync: unfiltered
        await EnsureIndexAsync(conn, "PX_Profiles_UpdatedAt_Id", """
            ON "Profiles" ("UpdatedAt", "Id")
            """, ct);
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

    static async Task ExecuteAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 0 };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    static string? GetString(NpgsqlDataReader reader, string column)
    {
        return reader.IsDBNull(column) ? null : reader.GetString(column);
    }
}
