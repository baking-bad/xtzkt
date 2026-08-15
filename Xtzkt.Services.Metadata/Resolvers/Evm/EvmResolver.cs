using Xtzkt.Data.Models;
using Xtzkt.Services.Metadata.Models;
using Xtzkt.Services.Metadata.Services;
using Xtzkt.Services.Metadata.Utils;
using Xtzkt.Utils;
using Xtzkt.Utils.Abi;
using Xtzkt.Utils.Extensions;
using Xtzkt.Utils.Network;

namespace Xtzkt.Services.Metadata.Resolvers.Evm;

public class EvmResolver(StoreService store, MetadataService metadata, IConfiguration config, ILogger<EvmResolver> logger) : IHostedService
{
    const string Erc20NameSelector = "0x06fdde03";       // name()
    const string Erc20SymbolSelector = "0x95d89b41";     // symbol()
    const string Erc20DecimalsSelector = "0x313ce567";   // decimals()
    const string Erc721TokenUriSelector = "0xc87b56dd";  // tokenURI(uint256)
    const string Erc1155TokenUriSelector = "0x0e89341c"; // uri(uint256)


    readonly StoreService _store = store;
    readonly MetadataService _metadata = metadata;
    readonly EvmResolverConfig _config = config.GetEvmResolverConfig();
    readonly ILogger _logger = logger;

    CancellationTokenSource? _cts;
    List<Task>? _tasks;

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("EvmResolver disabled");
            return;
        }

        if (_config.EvmNodes.Length == 0)
        {
            _logger.LogWarning("EvmResolver disabled: no evm nodes configured");
            return;
        }

        if (_config.EvmNodes.Any(x => x.MaxBatchSize < 3))
        {
            _logger.LogWarning("EvmResolver disabled: MaxBatchSize must be >= 3");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var chainIds = (await _store.GetChainsAsync(_cts.Token))
            .ToDictionary(x => x.ChainId, x => x.Id);

        var evmNodes = new Dictionary<int, List<EvmNodeConfig>>();
        foreach (var nodeConfig in _config.EvmNodes)
        {
            using var node = new EvmRpcClient(nodeConfig.Url, nodeConfig.Timeout, nodeConfig.ApiKeyHeader, nodeConfig.ApiKey);
            try
            {
                var chainId = await node.EthChainIdAsync(_cts.Token);

                if (!chainIds.TryGetValue(chainId, out var id))
                {
                    _logger.LogWarning("Evm node '{url}' has unknown chain_id and will be ignored", nodeConfig.Url);
                    continue;
                }

                if (!evmNodes.TryGetValue(id, out var nodes))
                {
                    nodes = [];
                    evmNodes.Add(id, nodes);
                }

                nodes.Add(nodeConfig);
            }
            catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Evm node '{url}' is unreachable and will be ignored", nodeConfig.Url);
                continue;
            }
        }

        if (evmNodes.Count == 0)
        {
            _logger.LogWarning("EvmResolver disabled: no evm nodes configured");
            return;
        }

        _logger.LogInformation("Ensure DB indexes");
        await _store.EnsureEvmResolverIndexes(ct);

        _tasks = new(evmNodes.Values.Sum(x => x.Count + 1));
        foreach (var (chainId, nodes) in evmNodes)
        {
            var pending = new Queue<TokenInfo>(_config.MaxQueue);
            var resolving = new HashSet<long>(_config.MaxQueue);
            var crit = new Lock();

            _logger.LogInformation("Start poller for chain #{chainId}", chainId);
            _tasks.Add(Task.Run(() => Poller(chainId, pending, resolving, crit, _cts.Token), _cts.Token));

            _logger.LogInformation("Start {cnt} worker(s) for chain #{chainId}", nodes.Count, chainId);
            foreach (var node in nodes)
                _tasks.Add(Task.Run(() => Resolver(node, pending, resolving, crit, _cts.Token), _cts.Token));
        }

        _logger.LogInformation("EvmResolver started");
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
        _logger.LogInformation("EvmResolver stopped");
    }

    async Task Poller(int chainId, Queue<TokenInfo> pending, HashSet<long> resolving, Lock crit, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(_config.SyncPeriod);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                int freeSpace;
                long[] exclude;
                lock (crit)
                {
                    freeSpace = _config.MaxQueue - resolving.Count;
                    exclude = freeSpace > 0 && resolving.Count != 0 ? [.. resolving] : [];
                }
                if (freeSpace <= 0)
                {
                    await Sleep(delay, ct);
                    continue;
                }

                var tokens = await _store.GetEvmPendingTokensAsync(chainId, freeSpace, exclude, _config.RetryDelays, ct);
                if (tokens.Count == 0)
                {
                    await Sleep(delay, ct);
                    continue;
                }

                lock (crit)
                {
                    foreach (var token in tokens)
                    {
                        pending.Enqueue(token);
                        resolving.Add(token.Id);
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

    async Task Resolver(EvmNodeConfig nodeConfig, Queue<TokenInfo> pending, HashSet<long> resolving, Lock crit, CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(500);
        var tokens = new List<TokenInfo>(nodeConfig.MaxBatchSize);
        var calls = new List<EthCallParams>(nodeConfig.MaxBatchSize);
        var rps = new RateLimiter(nodeConfig.MaxRps);
        using var node = new EvmRpcClient(nodeConfig.Url, nodeConfig.Timeout, nodeConfig.ApiKeyHeader, nodeConfig.ApiKey);

        static int callsCount(TokenInfo token) => (token.Tags & TokenTags.Erc20) == TokenTags.Erc20 ? 3 : 1;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                #region try dequeue
                lock (crit)
                {
                    while (pending.TryPeek(out var token) && calls.Count + callsCount(token) <= nodeConfig.MaxBatchSize)
                    {
                        tokens.Add(token);
                        if ((token.Tags & TokenTags.Erc20) == TokenTags.Erc20)
                        {
                            calls.Add(new(token.Contract, Erc20NameSelector));
                            calls.Add(new(token.Contract, Erc20SymbolSelector));
                            calls.Add(new(token.Contract, Erc20DecimalsSelector));
                        }
                        else if ((token.Tags & TokenTags.Erc721) == TokenTags.Erc721)
                        {
                            calls.Add(new(token.Contract, EvmValueEncoder.EncodeCallData(Erc721TokenUriSelector, token.TokenId)));
                        }
                        else if ((token.Tags & TokenTags.Erc1155) == TokenTags.Erc1155)
                        {
                            calls.Add(new(token.Contract, EvmValueEncoder.EncodeCallData(Erc1155TokenUriSelector, token.TokenId)));
                        }
                        else
                        {
                            // should never get here
                            throw new InvalidOperationException("Invalid evm token tags");
                        }
                        pending.Dequeue();
                    }
                }

                if (tokens.Count == 0)
                {
                    await Sleep(delay, ct);
                    continue;
                }
                #endregion

                #region fetch metadata
                var metadata = new List<TokenMetadata>(tokens.Count);

                await rps.AcquireAsync(ct);

                _logger.LogDebug("Executing {cnt1} call(s) for {cnt2} token(s) on '{url}'...", calls.Count, tokens.Count, nodeConfig.Url);
                try
                {
                    var callResults = await node.BatchEthCallAsync(calls, ct);
                    var syncedAt = DateTime.UtcNow.TrimMilliseconds();

                    var offset = 0;
                    foreach (var token in tokens)
                    {
                        if ((token.Tags & TokenTags.Erc20) == TokenTags.Erc20)
                        {
                            metadata.Add(ProcessErc20Calls(token, callResults, offset, syncedAt));
                            offset += 3;
                        }
                        else if ((token.Tags & TokenTags.Erc721) == TokenTags.Erc721)
                        {
                            metadata.Add(ProcessErc721Calls(token, callResults, offset, syncedAt));
                            offset += 1;
                        }
                        else
                        {
                            metadata.Add(ProcessErc1155Calls(token, callResults, offset, syncedAt));
                            offset += 1;
                        }
                    }

                    _logger.LogDebug("{cnt} token(s) resolved", metadata.Count);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve {cnt} token(s)", tokens.Count);

                    var syncedAt = DateTime.UtcNow.TrimMilliseconds();
                    foreach (var token in tokens)
                        metadata.Add(new TokenMetadata(token.Id, NextStatus(token), syncedAt));
                }
                #endregion

                #region save
                _logger.LogDebug("Saving {cnt} token(s)...", metadata.Count);
                try
                {
                    var saved = await _store.SaveAsync(metadata, ct);
                    _logger.LogDebug("{cnt} token(s) saved", saved);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to save");
                }

                lock (crit)
                {
                    foreach (var t in tokens)
                        resolving.Remove(t.Id);
                }

                calls.Clear();
                tokens.Clear();
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

    TokenMetadataStatus NextStatus(TokenInfo token)
    {
        return (int)token.Status >= _config.RetryDelays.Length
            ? TokenMetadataStatus.FailedToFetch
            : token.Status + 1;
    }

    TokenMetadata ProcessErc20Calls(TokenInfo token, EthCallResult[] results, int i, DateTime syncedAt)
    {
        try
        {
            if (results[i].Status == EthCallStatus.Fail ||
                results[i + 1].Status == EthCallStatus.Fail ||
                results[i + 2].Status == EthCallStatus.Fail)
                return new TokenMetadata(token.Id, NextStatus(token), syncedAt);

            string? name = null;
            string? symbol = null;
            int? decimals = null;

            if (results[i] is { Status: EthCallStatus.Success, Data: { } nameData } &&
                EvmValueDecoder.TryDecodeString(nameData, out var n) && n.Length > 0)
                name = n.Replace((char)0, Regexes.NullEscapeChar);

            if (results[i + 1] is { Status: EthCallStatus.Success, Data: { } symbolData } &&
                EvmValueDecoder.TryDecodeString(symbolData, out var s) && s.Length > 0)
                symbol = s.Replace((char)0, Regexes.NullEscapeChar);

            if (results[i + 2] is { Status: EthCallStatus.Success, Data: { } decimalsData } &&
                EvmValueDecoder.TryDecodeByte(decimalsData, out var d))
                decimals = d;

            return new TokenMetadata(token.Id, TokenMetadataStatus.Ok, syncedAt, name, symbol, decimals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process calls for token #{id}", token.Id);
            return new TokenMetadata(token.Id, TokenMetadataStatus.FailedToDecode, syncedAt);
        }
    }

    TokenMetadata ProcessErc721Calls(TokenInfo token, EthCallResult[] results, int i, DateTime syncedAt)
    {
        try
        {
            if (results[i].Status == EthCallStatus.Fail)
                return new TokenMetadata(token.Id, NextStatus(token), syncedAt);

            string? uri = null;

            if (results[i] is { Status: EthCallStatus.Success, Data: { } uriData } &&
                EvmValueDecoder.TryDecodeString(uriData, out var u) && u.Length > 0)
                uri = u;

            if (uri == null)
                return new TokenMetadata(token.Id, TokenMetadataStatus.Ok, syncedAt);

            if (uri.StartsWith("ipfs://", StringComparison.OrdinalIgnoreCase))
            {
                var cid = uri[7..];
                return IsFetchableIpfsPath(cid)
                    ? new TokenMetadata(token.Id, TokenMetadataStatus.Pending, SyncedAt: null, Link: "ipfs://" + cid)
                    : new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
            }

            if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return IsValidHttpUrl(uri)
                    ? new TokenMetadata(token.Id, TokenMetadataStatus.Pending, SyncedAt: null, Link: "http://" + uri[7..])
                    : new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
            }

            if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return IsValidHttpUrl(uri)
                    ? new TokenMetadata(token.Id, TokenMetadataStatus.Pending, SyncedAt: null, Link: "https://" + uri[8..])
                    : new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
            }

            if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return _metadata.FromDataUri(uri, token, syncedAt, false);

            return new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process calls for token #{id}", token.Id);
            return new TokenMetadata(token.Id, TokenMetadataStatus.FailedToDecode, syncedAt);
        }
    }

    TokenMetadata ProcessErc1155Calls(TokenInfo token, EthCallResult[] results, int i, DateTime syncedAt)
    {
        try
        {
            if (results[i].Status == EthCallStatus.Fail)
                return new TokenMetadata(token.Id, NextStatus(token), syncedAt);

            string? uri = null;

            if (results[i] is { Status: EthCallStatus.Success, Data: { } uriData } &&
                EvmValueDecoder.TryDecodeString(uriData, out var u) && u.Length > 0)
                uri = u;

            if (uri == null)
                return new TokenMetadata(token.Id, TokenMetadataStatus.Ok, syncedAt);
            
            if (uri.StartsWith("ipfs://", StringComparison.OrdinalIgnoreCase))
            {
                var cid = uri[7..];
                return IsFetchableIpfsPath(cid)
                    ? new TokenMetadata(token.Id, TokenMetadataStatus.Pending, SyncedAt: null, Link: "ipfs://" + cid)
                    : new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
            }

            if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return IsValidHttpUrl(uri)
                    ? new TokenMetadata(token.Id, TokenMetadataStatus.Pending, SyncedAt: null, Link: "http://" + uri[7..])
                    : new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
            }

            if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return IsValidHttpUrl(uri)
                    ? new TokenMetadata(token.Id, TokenMetadataStatus.Pending, SyncedAt: null, Link: "https://" + uri[8..])
                    : new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
            }

            if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return _metadata.FromDataUri(uri, token, syncedAt, true);

            return new TokenMetadata(token.Id, TokenMetadataStatus.InvalidUri, syncedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process calls for token #{id}", token.Id);
            return new TokenMetadata(token.Id, TokenMetadataStatus.FailedToDecode, syncedAt);
        }
    }

    static bool IsFetchableIpfsPath(string path)
    {
        // SSRF protection
        return path.Length > 0 &&
            path[0] != '/' &&
            path[0] != '\\' &&
            !Uri.TryCreate(path, UriKind.Absolute, out _);
    }

    static bool IsValidHttpUrl(string url)
    {
        // replacing placeholder for more accurate validation
        return Uri.TryCreate(url.Replace("{id}", "0"), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            !string.IsNullOrEmpty(uri.Host);
    }

    static async Task Sleep(TimeSpan ts, CancellationToken ct)
    {
        try { await Task.Delay(ts, ct); }
        catch (OperationCanceledException) { }
    }
}
