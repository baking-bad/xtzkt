using Dapper;
using Npgsql;
using System.Text;

namespace Xtzkt.Api.Services.Database;

public class DbInitService(
    NpgsqlDataSource dataSource,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<DbInitService> logger) : BackgroundService
{
    public bool PgTrgm { get; private set; }

    readonly NpgsqlDataSource _dataSource = dataSource;
    readonly ILogger _logger = logger;
    readonly string? _script = config.GetDbInitConfig().Script is string scriptPath
        ? Path.Combine(env.ContentRootPath, scriptPath)
        : null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var db = await _dataSource.OpenConnectionAsync(stoppingToken);

            if (_script == null)
            {
                _logger.LogInformation("DB init script not provided");
                await CheckExtensions(db, stoppingToken);
                return;
            }

            if (!File.Exists(_script))
            {
                _logger.LogInformation("DB init script not found at '{path}'", _script);
                await CheckExtensions(db, stoppingToken);
                return;
            }

            var statements = SplitStatements(await File.ReadAllTextAsync(_script, stoppingToken));
            if (statements.Count == 0)
            {
                _logger.LogInformation("DB init script at '{path}' doesn't contain any statements", _script);
                await CheckExtensions(db, stoppingToken);
                return;
            }

            // init extensions before long-running script
            await CheckExtensions(db, stoppingToken, false);

            _logger.LogInformation("Executing {cnt} statements from DB init script at '{path}'...", statements.Count, _script);
            foreach (var statement in statements)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("DB init script interrupted");
                    return;
                }
                try
                {
                    await db.ExecuteAsync(statement, commandTimeout: 0);
                    _logger.LogInformation("Statement '{s}...' executed", Shorten(statement));
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
                {
                    _logger.LogWarning("Statement '{s}...' not permitted", Shorten(statement));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Statement '{s}...' failed", Shorten(statement));
                }
            }

            _logger.LogInformation("DB init script executed");

            // re-read extensions after the init script
            await CheckExtensions(db, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbInitService crashed");
        }
    }

    async Task CheckExtensions(NpgsqlConnection db, CancellationToken stoppingToken, bool notify = true)
    {
        try
        {
            var extensions = await db.QueryAsync<string>("SELECT extname FROM pg_extension");
            PgTrgm = extensions.Contains("pg_trgm");

            if (notify && !PgTrgm)
                _logger.LogWarning("Postgres extension 'pg_trgm' is not installed, fuzzy search will be disabled");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check DB extensions, some functionality can be disabled");
        }
    }

    static List<string> SplitStatements(string script)
    {
        var res = new List<string>();
        var sb = new StringBuilder();

        foreach (var line in script.Split('\n').Where(x => !x.TrimStart().StartsWith("--")))
        {
            var ln = line.TrimEnd('\r');
            if (ln.Trim().Length != 0)
            {
                sb.AppendLine(ln);
            }
            else if (sb.Length != 0)
            {
                res.Add(sb.ToString());
                sb.Clear();
            }
        }
        
        if (sb.Length != 0)
        {
            res.Add(sb.ToString());
            sb.Clear();
        }

        return res;
    }

    static string Shorten(string statement)
    {
        var newLine = statement.IndexOf('\r');
        if (newLine == -1) newLine = statement.IndexOf('\n');
        if (newLine == -1) return statement;
        return statement[..newLine];
    }
}
