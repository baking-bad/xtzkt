using Dapper;
using Npgsql;
using System.Data;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Services.ResponseCache;
using Xtzkt.Data;

namespace Xtzkt.Api.Services.Database;

public class DbListenerService(
    ChainCache _chainCache,
    AddressCache _addressCache,
    ProtocolCache _protocolCache,
    ResponseCacheService _responseCache,
    IConfiguration _config,
    ILogger<DbListenerService> _logger) : BackgroundService
{
    #region channels
    const string StateHashChanged = "state_hash_changed";
    const string SyncStateChanged = "sync_state_changed";
    #endregion

    readonly Lock Crit = new();
    readonly List<int>[] StateChanges = [[], [], [], [], [], [], [], []];
    Task StateNotifying = Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("DB listener started");

            // no statement timeout: this connection sits in LISTEN and must not be capped
            var connectionString = _config.GetDbConnectionString(statementTimeout: false);

            using var db = new NpgsqlConnection(connectionString);
            db.Notification += OnNotification;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (db.State != ConnectionState.Open)
                    {
                        await db.OpenAsync(cancellationToken);
                        await db.ExecuteAsync($"""
                            LISTEN {StateHashChanged};
                            LISTEN {SyncStateChanged};
                            """);
                        _logger.LogInformation("Db listener connected");
                    }
                    await db.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DB listener disconnected");
                    try { await Task.Delay(1000, cancellationToken); }
                    catch (OperationCanceledException) { }
                }
            }

            db.Notification -= OnNotification;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "DB listener crashed");
        }
        finally
        {
            _logger.LogWarning("DB listener stopped");
        }
    }

    void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        _logger.LogDebug("Received {channel} notification with payload {payload}", e.Channel, e.Payload);

        if (e.Payload == null)
        {
            _logger.LogCritical("Invalid trigger payload");
            return;
        }

        if (e.Channel == StateHashChanged)
        {
            var data = e.Payload.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (data.Length != 3 ||
                !int.TryParse(data[0], out var id) ||
                !int.TryParse(data[1], out var level))
            {
                _logger.LogCritical("Invalid trigger payload");
                return;
            }

            lock (Crit)
            {
                StateChanges[id].Add(level);

                if (StateNotifying.IsCompleted)
                    StateNotifying = NotifyStateAsync(); // async run
            }
        }
        else if (e.Channel == SyncStateChanged)
        {
            var ind1 = e.Payload.IndexOf(':');
            var ind2 = e.Payload.IndexOf(':', ind1 + 1);
            if (ind2 == -1 ||
                !int.TryParse(e.Payload[..ind1], out var id) ||
                !int.TryParse(e.Payload[(ind1 + 1)..ind2], out var knownLevel) ||
                !DateTimeOffset.TryParse(e.Payload[(ind2 + 1)..], out var syncedAt))
            {
                _logger.LogCritical("Invalid trigger payload");
                return;
            }

            _chainCache.OnSyncStateChanged(id, knownLevel, syncedAt.UtcDateTime);
        }
        else
        {
            NotifyExtras(e.Channel, e.Payload);
        }
    }

    async Task NotifyStateAsync()
    {
        try
        {
            _logger.LogDebug("Processing state notification...");

            #region peek changes
            List<(int id, int min, int last)> changes = new(2);
            lock (Crit)
            {
                for (var i = 0; i < StateChanges.Length; i++)
                {
                    if (StateChanges[i].Count != 0)
                    {
                        changes.Add((i, StateChanges[i].Min(), StateChanges[i][^1]));
                        StateChanges[i].Clear();
                    }
                }
            }
            #endregion

            #region cache
            var tasks = new List<Task>(1);
            foreach (var (chainid, minLevel, lastLevel) in changes)
            {
                tasks.Add(_chainCache.OnStateChanged(chainid));
                tasks.Add(_addressCache.OnStateChanged(chainid, minLevel, lastLevel));
                tasks.Add(_protocolCache.OnStateChanged(chainid, minLevel, lastLevel));
            }
            await Task.WhenAll(tasks);

            _responseCache.Clear();
            #endregion

            #region ws
            // TODO
            #endregion

            #region home
            // TODO
            #endregion

            _logger.LogDebug("State notification processed");

            lock (Crit)
            {
                if (StateChanges.Any(x => x.Count != 0))
                {
                    _logger.LogDebug("Handle pending state notification");
                    StateNotifying = NotifyStateAsync(); // async run
                }
                else
                {
                    StateNotifying = Task.CompletedTask;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process state notification");
        }
    }

    void NotifyExtras(string channel, string payload)
    {
        try
        {
            _logger.LogDebug("Processing extras notification...");

            var ind = payload.IndexOf(':');
            if (ind == -1)
            {
                _logger.LogError("Invalid extras notification payload");
                return;
            }

            var key = payload[0..ind];
            var value = payload[(ind + 1)..];
            if (value.Length == 0) value = null;

            switch (channel)
            {
                default:
                    break;
            }

            _logger.LogDebug("Extras notification processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process extras notification");
        }
    }
}
