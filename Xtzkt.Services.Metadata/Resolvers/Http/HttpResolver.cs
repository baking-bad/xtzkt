using System.Diagnostics.CodeAnalysis;
using Xtzkt.Data.Models;
using Xtzkt.Services.Metadata.Models;
using Xtzkt.Services.Metadata.Services;
using Xtzkt.Services.Metadata.Utils;
using Xtzkt.Utils.Extensions;
using Xtzkt.Utils.Network;

namespace Xtzkt.Services.Metadata.Resolvers.Http;

public class HttpResolver(
    StoreService store,
    MetadataService metadata,
    IConfiguration config,
    ILogger<HttpResolver> logger) : IHostedService
{
    readonly StoreService _store = store;
    readonly MetadataService _metadata = metadata;
    readonly HttpResolverConfig _config = config.GetHttpResolverConfig();
    readonly ILogger _logger = logger;

    readonly Queue<TokenLinkInfo> _pending = new(config.GetHttpResolverConfig().MaxQueue);
    readonly HashSet<long> _resolving = new(config.GetHttpResolverConfig().MaxQueue);
    readonly List<TokenMetadata> _saving = new(config.GetHttpResolverConfig().SaveBatch);
    readonly RateLimiter _rps = new(config.GetHttpResolverConfig().MaxRps);
    readonly SafeHttpClient _client = new();
    readonly Lock _crit = new();

    CancellationTokenSource? _cts;
    List<Task>? _tasks;

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("HttpResolver disabled");
            return;
        }

        if (_config.Workers <= 0)
        {
            _logger.LogWarning("HttpResolver disabled: no workers configured");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _logger.LogInformation("Ensure DB indexes");
        await _store.EnsureHttpResolverIndexes(ct);

        _tasks = new(_config.Workers + 1);

        _logger.LogInformation("Start poller");
        _tasks.Add(Task.Run(() => Poller(_cts.Token), _cts.Token));

        _logger.LogInformation("Start {cnt} worker(s)", _config.Workers);
        for (int i = 0; i < _config.Workers; i++)
            _tasks.Add(Task.Run(() => Resolver(_cts.Token), _cts.Token));

        _logger.LogInformation("HttpResolver started");
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();

            if (_tasks != null)
                try { await Task.WhenAll(_tasks); } catch { }

            _cts.Dispose();
        }

        _client.Dispose();

        _logger.LogInformation("HttpResolver stopped");
    }

    async Task Poller(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(_config.SyncPeriod);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                int freeSpace;
                long[] exclude;
                lock (_crit)
                {
                    freeSpace = _config.MaxQueue - _resolving.Count;
                    exclude = freeSpace > 0 && _resolving.Count != 0 ? [.. _resolving] : [];
                }
                if (freeSpace <= 0)
                {
                    await Sleep(delay, ct);
                    continue;
                }

                var tokens = await _store.GetPendingLinksAsync("http", freeSpace, exclude, _config.RetryDelays, ct);
                if (tokens.Count == 0)
                {
                    await Sleep(delay, ct);
                    continue;
                }

                lock (_crit)
                {
                    foreach (var token in tokens)
                    {
                        _pending.Enqueue(token);
                        _resolving.Add(token.Id);
                    }
                }

                _logger.LogDebug("{cnt} tokens enqueued", tokens.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Poller failed");
                await Sleep(delay, ct);
            }
        }
    }

    async Task Resolver(CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(500);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                #region try dequeue
                if (!TryDequeue(out var token))
                {
                    await Sleep(delay, ct);
                    continue;
                }
                #endregion

                #region fetch metadata
                TokenMetadata metadata;

                var isErc1155 = token.Tags.HasFlag(TokenTags.Erc1155);
                var url = isErc1155
                    ? token.Link.Replace("{id}", Erc1155.TokenIdToHex64(token.TokenId))
                    : token.Link;

                var retry = (int)token.Status;
                var timeout = retry > 0 && retry <= _config.RetryTimeouts.Length
                    ? TimeSpan.FromSeconds(_config.RetryTimeouts[retry - 1])
                    : TimeSpan.FromSeconds(_config.Timeout);

                await _rps.AcquireAsync(ct);

                _logger.LogDebug("Fetching #{id} from {url} with timeout {ts:0.#}s...", token.Id, url, timeout.TotalSeconds);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                try
                {
                    using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    metadata = await _metadata.FromHttpResponse(response, token, DateTime.UtcNow.TrimMilliseconds(), isErc1155, cts.Token);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    _logger.LogDebug("Failed to fetch #{id}, request timeout", token.Id);
                    metadata = new TokenMetadata(token.Id, token.Status + 1, DateTime.UtcNow.TrimMilliseconds());
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch #{id}", token.Id);
                    metadata = new TokenMetadata(token.Id, token.Status + 1, DateTime.UtcNow.TrimMilliseconds());
                }

                if (metadata.Status < TokenMetadataStatus.FailedToFetch && (int)metadata.Status > _config.RetryDelays.Length)
                {
                    _logger.LogDebug("No retries left for #{id}", token.Id);
                    metadata.Status = TokenMetadataStatus.FailedToFetch;
                }
                #endregion

                #region save
                List<TokenMetadata>? toSave = null;
                lock (_crit)
                {
                    _saving.Add(metadata);
                    if (_saving.Count == _config.SaveBatch || _saving.Count == _resolving.Count)
                    {
                        toSave = [.. _saving];
                        _saving.Clear();
                    }
                }
                if (toSave != null)
                {
                    _logger.LogDebug("Saving {cnt} token(s)...", toSave.Count);
                    try
                    {
                        var saved = await _store.SaveAsync(toSave, ct);
                        _logger.LogDebug("{cnt} token(s) saved", saved);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to save");
                    }

                    lock (_crit)
                    {
                        foreach (var t in toSave)
                            _resolving.Remove(t.Id);
                    }
                }
                #endregion
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // should never get here
                _logger.LogError(ex, "Resolver failed");
                await Sleep(delay, ct);
            }
        }
    }

    bool TryDequeue([NotNullWhen(true)] out TokenLinkInfo? token)
    {
        lock (_crit)
        {
            return _pending.TryDequeue(out token);
        }
    }

    static async Task Sleep(TimeSpan ts, CancellationToken ct)
    {
        try { await Task.Delay(ts, ct); }
        catch (OperationCanceledException) { }
    }
}
