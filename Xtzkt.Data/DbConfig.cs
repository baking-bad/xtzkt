using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Xtzkt.Data;

public class DbConfig
{
    /// <summary>
    /// Connection string without timeouts - those are separate settings below, so that a deployment
    /// can tune them without rewriting the whole string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Client-side limit (in seconds) for a single command. Npgsql enforces it by sending a cancel
    /// request from the client, so it doesn't survive the process dying.
    /// </summary>
    public int CommandTimeout { get; set; } = 60;

    /// <summary>
    /// Server-side limit (in seconds) for a single statement. Postgres enforces it itself, so unlike
    /// <see cref="CommandTimeout"/> it also fires when the client is gone, or when a cancel request
    /// can't reach the backend. It applies to every statement on the connection, migrations and
    /// index builds included, so the indexers normally leave it at 0, which disables it.
    /// </summary>
    public int StatementTimeout { get; set; } = 58;

    /// <summary>
    /// Path to the SQL script to execute on startup (<c>Xtzkt.Api</c> only).
    /// </summary>
    public string? InitScript { get; set; } = "init.pgsql";

    /// <summary>
    /// Connection string with the configured timeouts applied. Pass <c>statementTimeout: false</c>
    /// for connections that legitimately run long queries, such as cache warm-up.
    /// </summary>
    public string GetConnectionString(bool statementTimeout = true)
    {
        if (string.IsNullOrEmpty(ConnectionString))
            throw new Exception("Db.ConnectionString is missed");

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            CommandTimeout = CommandTimeout
        };

        if (statementTimeout && StatementTimeout > 0)
        {
            var option = $"-c statement_timeout={StatementTimeout * 1000}";
            builder.Options = string.IsNullOrEmpty(builder.Options) ? option : $"{builder.Options} {option}";
        }

        return builder.ToString();
    }
}

public static class DbConfigExt
{
    public static DbConfig GetDbConfig(this IConfiguration config)
    {
        return config.GetSection("Db")?.Get<DbConfig>() ?? new();
    }

    public static string GetDbConnectionString(this IConfiguration config, bool statementTimeout = true)
    {
        return config.GetDbConfig().GetConnectionString(statementTimeout);
    }
}
