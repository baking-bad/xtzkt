using System.Diagnostics.CodeAnalysis;
using Xtzkt.Data.Models;
using Xtzkt.Services.Metadata.Models;
using Xtzkt.Services.Metadata.Services;
using Xtzkt.Services.Metadata.Utils;
using Xtzkt.Utils.Extensions;
using Xtzkt.Utils.Network;

namespace Xtzkt.Services.Metadata.Resolvers.Ipfs;

public class IpfsResolver(
    StoreService store,
    MetadataService metadata,
    IConfiguration config,
    ILogger<IpfsResolver> logger) : IHostedService
{
    readonly StoreService _store = store;
    readonly MetadataService _metadata = metadata;
    readonly IpfsResolverConfig _config = config.GetIpfsResolverConfig();
    readonly ILogger _logger = logger;

    readonly Queue<TokenLinkInfo> _pending = new(config.GetIpfsResolverConfig().MaxQueue);
    readonly HashSet<long> _resolving = new(config.GetIpfsResolverConfig().MaxQueue);
    readonly List<TokenMetadata> _saving = new(config.GetIpfsResolverConfig().SaveBatch);
    readonly List<SafeHttpClient> _clients = new(config.GetIpfsResolverConfig().IpfsGateways.Length);
    readonly Lock _crit = new();

    CancellationTokenSource? _cts;
    List<Task>? _tasks;

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("IpfsResolver disabled");
            return;
        }

        var ipfsGateways = _config.IpfsGateways.Where(x => x.Workers > 0);
        var ipfsWorkers = ipfsGateways.Sum(x => x.Workers);
        if (ipfsWorkers == 0)
        {
            _logger.LogWarning("IpfsResolver disabled: no ipfs gateways configured");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _logger.LogInformation("Ensure DB indexes");
        await _store.EnsureIpfsResolverIndexes(ct);

        _tasks = new(ipfsWorkers + 1);

        _logger.LogInformation("Start poller");
        _tasks.Add(Task.Run(() => Poller(_cts.Token), _cts.Token));

        foreach (var gateway in ipfsGateways)
        {
            _logger.LogInformation("Start {cnt} worker(s) for '{url}'", gateway.Workers, gateway.Url);

            var rps = new RateLimiter(gateway.MaxRps);
            var client = new SafeHttpClient(gateway.Url);
            _clients.Add(client);

            for (var i = 0; i < gateway.Workers; i++)
                _tasks.Add(Task.Run(() => Resolver(gateway, client, rps, _cts.Token), _cts.Token));
        }

        _logger.LogInformation("IpfsResolver started");
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

        foreach (var client in _clients)
            client.Dispose();

        _logger.LogInformation("IpfsResolver stopped");
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

                var tokens = await _store.GetPendingLinksAsync("ipfs", freeSpace, exclude, _config.RetryDelays, ct);
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

    async Task Resolver(IpfsGatewayConfig ipfsConfig, SafeHttpClient ipfs, RateLimiter rps, CancellationToken ct)
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
                var path = isErc1155
                    ? token.Link[7..].Replace("{id}", Erc1155.TokenIdToHex64(token.TokenId))
                    : token.Link[7..];

                var retry = (int)token.Status;
                var timeout = retry > 0 && retry <= _config.RetryTimeouts.Length
                    ? TimeSpan.FromSeconds(_config.RetryTimeouts[retry - 1])
                    : TimeSpan.FromSeconds(ipfsConfig.Timeout);

                await rps.AcquireAsync(ct);

                _logger.LogDebug("Fetching #{id} from {url}{path} with timeout {ts:0.#}s...", token.Id, ipfsConfig.Url, path, timeout.TotalSeconds);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                try
                {
                    using var response = await ipfs.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    metadata = await _metadata.FromHttpResponse(response, token, DateTime.UtcNow.TrimMilliseconds(), token.Tags.HasFlag(TokenTags.Erc1155), cts.Token);
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
