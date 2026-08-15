using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Services.Metadata.Models;
using Xtzkt.Services.Metadata.Services;
using Xtzkt.Utils.Extensions;

namespace Xtzkt.Services.Metadata.Resolvers.DipDup;

public class DipDupResolver(StoreService store, MetadataService metadata, IConfiguration config, ILogger<DipDupResolver> logger) : IHostedService
{
    readonly StoreService _store = store;
    readonly MetadataService _metadata = metadata;
    readonly DipDupResolverConfig _config = config.GetDipDupResolverConfig();
    readonly ILogger _logger = logger;

    CancellationTokenSource? _cts;
    List<DipDupClient>? _clients;
    List<Task>? _tasks;

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("DipDupResolver disabled");
            return;
        }

        if (_config.Sources.Length == 0)
        {
            _logger.LogWarning("DipDupResolver disabled: no sources configured");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var chains = (await _store.GetChainsAsync(_cts.Token))
            .Select(x => x.Id)
            .ToHashSet();

        var sources = _config.Sources
            .Where(x => chains.Contains(x.Chain))
            .Where(x => x.TokenMetadataTable != null || x.ContractMetadataTable != null)
            .GroupBy(x => x.Chain);

        if (!sources.Any())
        {
            _logger.LogWarning("DipDupResolver disabled: no valid sources configured");
            return;
        }

        _logger.LogInformation("Ensure DB indexes");
        await _store.EnsureDipDupResolverIndexes(ct);

        _clients = new(sources.Sum(x => x.Count()));
        _tasks = new(sources.Count());
        foreach (var configs in sources)
        {
            var clients = configs.Select(x => new DipDupClient(x)).ToArray();
            _clients.AddRange(clients);

            _logger.LogInformation("Start {cnt} worker(s) for chain #{chainId}", clients.Length, configs.Key);
            _tasks.Add(Task.Run(() => Resolver(configs.Key, clients, _cts.Token), _cts.Token));
        }

        _logger.LogInformation("DipDupResolver started");
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();

            if (_tasks != null)
                try { await Task.WhenAll(_tasks); } catch { }

            if (_clients != null)
                foreach (var client in _clients)
                    client.Dispose();

            _cts.Dispose();
        }

        _logger.LogInformation("DipDupResolver stopped");
    }

    async Task Resolver(int chainId, DipDupClient[] clients, CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(_config.SyncPeriod);
        var syncTokens = clients.Any(x => x.SyncTokenMetadata);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                #region sync updates
                var state = await GetStateAsync(chainId, ct);
                foreach (var client in clients)
                {
                    var sourceState = GetSourceState(state, client.Url);

                    var sentinel = await client.GetSentinelAsync(ct);
                    if (sourceState.Sentinel != sentinel)
                    {
                        if (sourceState.LastTokenUpdateId == -1 && sourceState.LastContractUpdateId == -1)
                        {
                            _logger.LogInformation("Sentinel initialized for '{url}'", client.Url);
                        }
                        else
                        {
                            _logger.LogWarning("Sentinel changed for '{url}', reset state", client.Url);
                            sourceState.LastTokenUpdateId = -1;
                            sourceState.LastContractUpdateId = -1;
                        }
                        sourceState.Sentinel = sentinel;
                        await SaveStateAsync(chainId, state, ct);
                    }

                    if (client.SyncContractMetadata)
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            var metadata = await client.GetContractMetadataAsync(sourceState.LastContractUpdateId, ct);
                            if (metadata.Count == 0) break;
                            _logger.LogDebug("{cnt} contract updates from '{url}' received", metadata.Count, client.Url);

                            var saved = await SaveDipDupContractMetadataAsync(chainId, metadata, ct);
                            _logger.LogDebug("{cnt} contract updates from '{url}' saved", saved, client.Url);

                            // some contracts aren't indexed by xtzkt yet — suspend without advancing the cursor,
                            // otherwise we'd skip them permanently (contracts have no backfill fallback)
                            if (saved != metadata.Count)
                            {
                                _logger.LogWarning("Contract sync suspended for '{url}' until the indexer catches up", client.Url);
                                break;
                            }

                            sourceState.LastContractUpdateId = metadata[^1].UpdateId;
                            await SaveStateAsync(chainId, state, ct);
                            _logger.LogDebug("State for '{url}' updated, lastContractUpdateId: {id}", client.Url, sourceState.LastContractUpdateId);

                            if (metadata.Count < client.QueryLimit) break;
                        }
                    }

                    if (client.SyncTokenMetadata)
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            var metadata = await client.GetTokenMetadataAsync(sourceState.LastTokenUpdateId, ct);
                            if (metadata.Count == 0) break;
                            _logger.LogDebug("{cnt} token updates from '{url}' received", metadata.Count, client.Url);

                            var saved = await SaveDipDupTokenMetadataAsync(chainId, metadata, ct);
                            _logger.LogDebug("{cnt} token updates from '{url}' saved", saved, client.Url);

                            sourceState.LastTokenUpdateId = metadata[^1].UpdateId;
                            await SaveStateAsync(chainId, state, ct);
                            _logger.LogDebug("State for '{url}' updated, lastTokenUpdateId: {id}", client.Url, sourceState.LastTokenUpdateId);

                            if (metadata.Count < client.QueryLimit) break;
                        }
                    }
                }
                #endregion

                #region backfill skipped tokens
                // token metadata can exist in dipdup, but the token can not yet exist xtzkt,
                // so at the sync stage the updates will be ignored, so when the token is
                // finally indexed in xtzkt, we check its metadata directly
                while (syncTokens && !ct.IsCancellationRequested)
                {
                    var tokens = await _store.GetPendingFaTokensAsync(chainId, _config.BackfillLimit, _config.RetryDelays, ct);
                    if (tokens.Count == 0) break;
                    _logger.LogDebug("{cnt} pending tokens selected", tokens.Count);

                    var ids = tokens.ToDictionary(x => (x.Contract, x.TokenId.ToString()), x => x.Id);
                    var keys = tokens.Select(x => (x.Contract, x.TokenId.ToString())).ToList();
                    var found = new HashSet<(string, string)>(tokens.Count);
                    foreach (var client in clients.Where(x => x.SyncTokenMetadata))
                    {
                        var metadata = await client.GetTokenMetadataAsync(keys, ct);
                        if (metadata.Count == 0) continue;
                        _logger.LogDebug("{cnt} token metadata found on '{url}'", metadata.Count, client.Url);

                        foreach (var m in metadata)
                            found.Add((m.Contract, m.TokenId));

                        var saved = await SaveDipDupTokenMetadataAsync(ids, metadata, ct);
                        _logger.LogDebug("{cnt} token metadata saved", saved);
                    }

                    if (found.Count != tokens.Count)
                    {
                        var syncedAt = DateTime.UtcNow.TrimMilliseconds();
                        var failedMetadata = tokens
                            .Where(t => !found.Contains((t.Contract, t.TokenId.ToString())))
                            .Select(t => new TokenMetadata(t.Id, (int)t.Status >= _config.RetryDelays.Length ? TokenMetadataStatus.FailedToFetch : t.Status + 1, syncedAt))
                            .ToList();

                        var saved = await _store.SaveAsync(failedMetadata, ct);
                        _logger.LogDebug("{cnt} token metadata touched", saved);
                    }

                    if (tokens.Count < _config.BackfillLimit) break;
                }
                #endregion
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resolver failed");
            }

            await Sleep(period, ct);
        }
    }

    async Task<int> SaveDipDupTokenMetadataAsync(Dictionary<(string, string), long> ids, List<DipDupTokenMetadata> updates, CancellationToken ct)
    {
        var metadata = new List<TokenMetadata>(updates.Count);
        foreach (var update in updates)
        {
            if (!ids.TryGetValue((update.Contract, update.TokenId), out var id))
                continue;

            var link = update.Link;
            var syncedAt = DateTime.UnixEpoch.AddSeconds(update.UpdatedAt);

            if (update.Status != 3)
            {
                metadata.Add(new TokenMetadata(id, TokenMetadataStatus.FailedToFetch, syncedAt, Link: link));
                continue;
            }

            var (status, name, symbol, decimals, json) = _metadata.FromJsonElement(update.Metadata);
            metadata.Add(new TokenMetadata(id, status, syncedAt, name, symbol, decimals, json, link));
        }

        return await _store.SaveAsync(metadata, ct);
    }

    async Task<int> SaveDipDupTokenMetadataAsync(int chainId, List<DipDupTokenMetadata> updates, CancellationToken ct)
    {
        var contractIds = await _store.GetContractIdsAsync(chainId, [.. updates.Select(x => x.Contract).Distinct()], ct);
        if (contractIds.Count == 0) return 0;

        var metadata = new List<TokenMetadataEx>(updates.Count);
        foreach (var update in updates)
        {
            if (!contractIds.TryGetValue(update.Contract, out var contractId))
                continue;

            var link = update.Link;
            var tokenId = update.TokenId;
            var syncedAt = DateTime.UnixEpoch.AddSeconds(update.UpdatedAt);

            if (update.Status != 3)
            {
                metadata.Add(new TokenMetadataEx(contractId, tokenId, TokenMetadataStatus.FailedToFetch, syncedAt, Link: link));
                continue;
            }

            var (status, name, symbol, decimals, json) = _metadata.FromJsonElement(update.Metadata);
            metadata.Add(new TokenMetadataEx(contractId, tokenId, status, syncedAt, name, symbol, decimals, json, link));
        }

        return await _store.SaveAsync(metadata, ct);
    }

    async Task<int> SaveDipDupContractMetadataAsync(int chainId, List<DipDupContractMetadata> updates, CancellationToken ct)
    {
        var contracts = new List<(string Hash, string? Json)>(updates.Count);
        foreach (var update in updates)
        {
            var (_, _, _, _, json) = _metadata.FromJsonElement(update.Metadata);
            contracts.Add((update.Contract, json));
        }

        return await _store.SaveDipDupContractMetadataAsync(chainId, contracts, ct);
    }

    async Task<int> SaveStateAsync(int chainId, DipDupResolverState state, CancellationToken ct)
    {
        return await _store.SaveDipDupResolverStateAsync(chainId, JsonSerializer.Serialize(state), ct);
    }

    async Task<DipDupResolverState> GetStateAsync(int chainId, CancellationToken ct)
    {
        var json = await _store.GetDipDupResolverStateAsync(chainId, ct);
        try { return json != null ? JsonSerializer.Deserialize<DipDupResolverState>(json) ?? new() : new(); }
        catch { return new(); }
    }

    static DipDupSourceState GetSourceState(DipDupResolverState state, string url)
    {
        if (!state.TokenSources.TryGetValue(url, out var sourceState))
        {
            sourceState = new();
            state.TokenSources.Add(url, sourceState);
        }
        return sourceState;
    }

    static async Task Sleep(TimeSpan ts, CancellationToken ct)
    {
        try { await Task.Delay(ts, ct); }
        catch (OperationCanceledException) { }
    }
}

public sealed class DipDupResolverState
{
    public Dictionary<string, DipDupSourceState> TokenSources { get; set; } = [];
}

public sealed class DipDupSourceState
{
    public long LastTokenUpdateId { get; set; } = -1;
    public long LastContractUpdateId { get; set; } = -1;
    public long Sentinel { get; set; } = 0;
}
