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
    DomainCache _domainCache,
    ProtocolCache _protocolCache,
    SoftwareCache _softwareCache,
    ResponseCacheService _responseCache,
    IConfiguration _config,
    ILogger<DbListenerService> _logger) : BackgroundService
{
    #region channels
    const string ChainStateChanged = "chain_state_changed";
    const string ChainSyncStateChanged = "chain_sync_state_changed";
    const string DomainChanged = "domain_changed";
    #endregion

    readonly Lock Crit = new();
    readonly List<int>[] StateChanges = [[], [], [], [], [], [], [], []];
    Task StateNotifying = Task.CompletedTask;
    readonly HashSet<long> DomainChanges = [];
    bool DomainReset;
    Task DomainNotifying = Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("DB listener started");

            // no statement timeout: this connection sits in LISTEN and must not be capped
            var connectionString = new NpgsqlConnectionStringBuilder(_config.GetDbConnectionString(statementTimeout: false)) { KeepAlive = 30 }.ToString();

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
                            LISTEN {ChainStateChanged};
                            LISTEN {ChainSyncStateChanged};
                            LISTEN {DomainChanged};
                            """);
                        _logger.LogInformation("Db listener connected");

                        // notifications sent while disconnected are lost, so caches should be reset
                        ResetCaches();
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

        if (e.Channel == ChainStateChanged)
        {
            var data = e.Payload.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (data.Length != 2 ||
                !int.TryParse(data[0], out var id) ||
                (uint)id >= StateChanges.Length ||
                !int.TryParse(data[1], out var level))
            {
                _logger.LogCritical("Invalid trigger payload");
                return;
            }

            lock (Crit)
            {
                StateChanges[id].Add(level);

                if (StateNotifying.IsCompleted)
                    StateNotifying = Task.Run(NotifyStateAsync); // async run
            }
        }
        else if (e.Channel == ChainSyncStateChanged)
        {
            var ind1 = e.Payload.IndexOf(':');
            var ind2 = e.Payload.IndexOf(':', ind1 + 1);
            if (ind2 == -1 ||
                !int.TryParse(e.Payload[..ind1], out var id) ||
                (uint)id >= StateChanges.Length ||
                !int.TryParse(e.Payload[(ind1 + 1)..ind2], out var knownLevel) ||
                !DateTimeOffset.TryParse(e.Payload[(ind2 + 1)..], out var syncedAt))
            {
                _logger.LogCritical("Invalid trigger payload");
                return;
            }

            _chainCache.OnSyncStateChanged(id, knownLevel, syncedAt.UtcDateTime);
        }
        else if (e.Channel == DomainChanged)
        {
            if (!long.TryParse(e.Payload, out var id))
            {
                _logger.LogCritical("Invalid {channel} trigger payload", e.Channel);
                return;
            }

            lock (Crit)
            {
                DomainChanges.Add(id);

                if (DomainNotifying.IsCompleted)
                    DomainNotifying = Task.Run(NotifyDomainsAsync); // async run
            }
        }
        else
        {
            NotifyExtras(e.Channel, e.Payload);
        }
    }

    async Task NotifyStateAsync()
    {
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

        try
        {
            _logger.LogDebug("Processing state notification...");

            #region cache
            var tasks = new List<Task>(changes.Count * 4);
            foreach (var (chainid, minLevel, lastLevel) in changes)
            {
                tasks.Add(_chainCache.OnStateChanged(chainid));
                tasks.Add(_addressCache.OnStateChanged(chainid, minLevel, lastLevel));
                tasks.Add(_protocolCache.OnStateChanged(chainid, minLevel, lastLevel));
                tasks.Add(_softwareCache.OnStateChanged(chainid, minLevel, lastLevel));
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process state notification");

            _responseCache.Clear();

            lock (Crit)
            {
                // retry, prepended so that the last level stays the latest one
                foreach (var (id, min, last) in changes)
                    StateChanges[id].InsertRange(0, [min, last]);
            }

            await Task.Delay(1000);
        }

        lock (Crit)
        {
            if (StateChanges.Any(x => x.Count != 0))
            {
                _logger.LogDebug("Handle pending state notification");
                StateNotifying = Task.Run(NotifyStateAsync); // async run
            }
            else
            {
                StateNotifying = Task.CompletedTask;
            }
        }
    }

    async Task NotifyDomainsAsync()
    {
        #region peek changes
        bool reset;
        long[] ids;
        lock (Crit)
        {
            reset = DomainReset;
            DomainReset = false;

            ids = [.. DomainChanges];
            DomainChanges.Clear();
        }
        #endregion

        try
        {
            _logger.LogDebug("Processing domain notifications...");

            if (reset)
                await _domainCache.ReloadAsync();
            else
                await _domainCache.UpdateAsync(ids);

            _responseCache.Clear();

            _logger.LogDebug("Domain notifications processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process domain notifications");

            lock (Crit)
            {
                // retry
                DomainReset |= reset;
                DomainChanges.UnionWith(ids);
            }

            await Task.Delay(1000);
        }

        lock (Crit)
        {
            if (DomainReset || DomainChanges.Count != 0)
            {
                _logger.LogDebug("Handle pending domain notification");
                DomainNotifying = Task.Run(NotifyDomainsAsync); // async run
            }
            else
            {
                DomainNotifying = Task.CompletedTask;
            }
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

    void ResetCaches()
    {
        lock (Crit)
        {
            DomainReset = true;
            if (DomainNotifying.IsCompleted)
                DomainNotifying = Task.Run(NotifyDomainsAsync); // async run
        }
    }
}
