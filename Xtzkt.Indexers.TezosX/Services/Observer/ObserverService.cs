using Microsoft.EntityFrameworkCore;
using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX.Services.Observer;
using Xtzkt.Utils.Extensions;

namespace Xtzkt.Indexers.TezosX.Services
{
    public class ObserverService(
        EvmNode _node,
        IServiceScopeFactory _services,
        IConfiguration _config,
        ILogger<ObserverService> _logger,
        IMetrics _metrics) : IHostedService
    {
        #region static
        const int SyncStatusTtl = 5;
        #endregion

        readonly CancellationTokenSource _cts = new();
        readonly HeadNotifier _headNotifier = HeadNotifier.Create(_config.GetObserverConfig(), _node, _logger);
        readonly Lock _lock = new();

        Task? _headNotifierTask;
        volatile Task? _updateTask;
        volatile Task? _applyTask;

        XChain _state = null!;
        volatile Header _head = Header.Empty();
        DateTime _syncedAt = DateTime.MinValue;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ResetState(cancellationToken);
                _logger.LogInformation("State initialized: [{level}:{hash1}:{hash2}]", _state.Level, _state.Hash, _state.MichelsonBlock);

                _headNotifier.OnHead += OnHead;
                _headNotifierTask = _headNotifier.RunAsync(_cts.Token);

                _logger.LogInformation("Synchronization started");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                // should never happen
                _logger.LogCritical(ex, "Observer crashed when starting");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _headNotifier.OnHead -= OnHead;

                _cts.Cancel();
                if (_headNotifierTask != null) await _headNotifierTask;
                if (_updateTask != null) await _updateTask;
                if (_applyTask != null) await _applyTask;
                _cts.Dispose();

                _logger.LogInformation("Synchronization stopped");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                // should never happen
                _logger.LogCritical(ex, "Observer crashed when stopping");
                throw;
            }
        }

        private void OnHead(Header head)
        {
            if (head.Hash != _head.Hash)
                _logger.LogDebug("New head [{level}:{hash}]", head.Level, head.Hash);

            lock (_lock)
            {
                _head = head;
                _syncedAt = DateTime.UtcNow;

                if (CanUpdateSyncStatus())
                    _updateTask ??= Task.Run(UpdateSyncStatus);

                if (CanApplyUpdates())
                    _applyTask ??= Task.Run(ApplyUpdates);
            }
        }

        private async Task UpdateSyncStatus()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    _state.KnownLevel = _head.Level;
                    _state.SyncedAt = _syncedAt.TrimMilliseconds();

                    _metrics.Measure.Gauge.SetHealthValue(_state);

                    using var scope = _services.CreateScope();
                    using var db = scope.ServiceProvider.GetRequiredService<XtzktContext>();

                    await db.Database.ExecuteSqlRawAsync("""
                        UPDATE "Chains"
                        SET "KnownLevel" = {0},
                            "SyncedAt" = {1}
                        WHERE "Id" = {2}
                        """, [_state.KnownLevel, _state.SyncedAt, _state.Id], _cts.Token);

                    _logger.LogDebug("Sync status updated");
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    // no big deal
                    _logger.LogError(ex, "Failed to update sync status");
                }

                lock (_lock)
                {
                    if (CanUpdateSyncStatus())
                        continue;

                    _updateTask = null;
                    return;
                }
            }
        }

        private async Task ApplyUpdates()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var head = _head;
                    if (head.Level <= _state.Level)
                    {
                        if (await IsLocalBranchValid(head))
                        {
                            // the node is behind, but on our branch, so there's nothing to rebase
                            lock (_lock)
                            {
                                if (_head.Hash != head.Hash)
                                    continue; // a newer head arrived while we were checking

                                _applyTask = null;
                                return;
                            }
                        }

                        _logger.LogWarning("Chain reorg detected. Rebase local branch...");
                        await RebaseLocalBranch(_cts.Token);
                    }

                    await AdvanceLocalBranch(_cts.Token);

                    _logger.LogDebug("Updates applied");
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply updates. Retry in 5 sec...");
                    try { await Task.Delay(5000, _cts.Token); }
                    catch (OperationCanceledException) { }
                }

                lock (_lock)
                {
                    if (CanApplyUpdates())
                        continue;

                    _applyTask = null;
                    return;
                }
            }
        }

        private async Task AdvanceLocalBranch(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var headLevel = _head.Level;
                if (_state.Level >= headLevel)
                    return;

                try
                {
                    _logger.LogDebug("Applying block...");
                    using (_metrics.Measure.Timer.Time(MetricsRegistry.ApplyBlockTime))
                    {
                        using var scope = _services.CreateScope();
                        var handler = scope.ServiceProvider.GetProtocolHandler(_state.Kernel);
                        _state = await handler.ApplyNextBlock(headLevel);
                    }
                    _metrics.Measure.Gauge.SetHealthValue(_state);
                    _logger.LogInformation("Applied {level} of {total}", _state.Level, _state.KnownLevel);
                }
                catch (BaseException ex) when (ex.RebaseRequired)
                {
                    _logger.LogWarning(ex, "Failed to apply block: rebase required. Rebase local branch...");
                    await ResetState(cancellationToken);
                    await RebaseLocalBranch(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply block. Retry in 5 sec...");
                    await Task.Delay(5000, cancellationToken);
                    await ResetState(cancellationToken);
                }
            }
        }

        private async Task RebaseLocalBranch(CancellationToken cancellationToken)
        {
            while (_state.Level >= 0 && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (await IsLocalBranchValid(_head)) return;

                    _logger.LogDebug("Reverting block...");
                    using (_metrics.Measure.Timer.Time(MetricsRegistry.RevertBlockTime))
                    {
                        using var scope = _services.CreateScope();
                        var handler = scope.ServiceProvider.GetProtocolHandler(_state.Kernel);
                        _state = await handler.RevertLastBlock();
                    }
                    _metrics.Measure.Gauge.SetHealthValue(_state);
                    _logger.LogInformation("Reverted to {level} of {total}", _state.Level, _state.KnownLevel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to revert block. Retry in 5 sec...");
                    await Task.Delay(5000, cancellationToken);
                    await ResetState(cancellationToken);
                }
            }
        }

        private async Task<bool> IsLocalBranchValid(Header head)
        {
            if (head.Level > _state.Level)
            {
                var remote = await _node.GetBlock(_state.Level);
                return remote.RequiredString("hash") == _state.Hash;
            }

            if (head.Level < _state.Level)
            {
                using var scope = _services.CreateScope();
                var cache = scope.ServiceProvider.GetRequiredService<CacheService>();
                var local = await cache.Blocks.GetAsync(head.Level);
                return local.Hash == head.Hash;
            }

            return head.Hash == _state.Hash;
        }

        private async Task ResetState(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var cache = scope.ServiceProvider.GetRequiredService<CacheService>();

                    await cache.ResetAsync();

                    _state = cache.Chain.Get();
                    _metrics.Measure.Gauge.SetHealthValue(_state);

                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reset state. Retry in 5 sec...");
                    await Task.Delay(5000, cancellationToken);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        private bool CanUpdateSyncStatus()
        {
            return _head.Level != _state.KnownLevel || _syncedAt >= _state.SyncedAt.AddSeconds(SyncStatusTtl);
        }

        private bool CanApplyUpdates()
        {
            return _head.Hash != _state.Hash;
        }
    }
}
