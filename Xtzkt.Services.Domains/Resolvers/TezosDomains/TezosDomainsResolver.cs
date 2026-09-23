using System.Data;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xtzkt.Services.Domains.Models;
using Xtzkt.Services.Domains.Services;
using Xtzkt.Utils;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Services.Domains.Resolvers.TezosDomains;

public class TezosDomainsResolver(StoreService store, IConfiguration config, ILogger<TezosDomainsResolver> logger) : IHostedService
{
    static readonly JsonSerializerOptions DataJsonOptions = new() { MaxDepth = 99 };
    static readonly JsonSerializerOptions WrappedDataJsonOptions = new() { MaxDepth = 100 };

    readonly StoreService _store = store;
    readonly TezosDomainsResolverConfig _config = config.GetTezosDomainsResolverConfig();
    readonly ILogger _logger = logger;

    CancellationTokenSource? _cts;
    List<Task>? _tasks;

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("TezosDomainsResolver disabled");
            return;
        }

        if (_config.SyncPeriod < 1)
        {
            _logger.LogWarning("TezosDomainsResolver disabled: SyncPeriod must be at least 1");
            return;
        }

        if (_config.BatchSize < 1)
        {
            _logger.LogWarning("TezosDomainsResolver disabled: BatchSize must be at least 1");
            return;
        }

        if (_config.ReorgDepth < 1)
        {
            _logger.LogWarning("TezosDomainsResolver disabled: ReorgDepth must be at least 1");
            return;
        }

        if (_config.Sources.Length == 0)
        {
            _logger.LogWarning("TezosDomainsResolver disabled: no sources configured");
            return;
        }

        var distinctSources = _config.Sources
            .DistinctBy(x => (x.Chain, x.NameRegistry))
            .ToList();

        if (distinctSources.Count < _config.Sources.Length)
            _logger.LogWarning("{cnt} duplicate source(s) ignored", _config.Sources.Length - distinctSources.Count);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var chains = await _store.GetChainIdsAsync(_cts.Token);
        var sources = new List<TezosDomainsSourceConfig>(distinctSources.Count);
        foreach (var source in distinctSources)
        {
            if (string.IsNullOrEmpty(source.NameRegistry))
                _logger.LogWarning("Source for chain #{chain} skipped: no name registry configured", source.Chain);
            else if (!source.NameRegistry.StartsWith("KT1") || !Regexes.MichelsonAddress().IsMatch(source.NameRegistry))
                _logger.LogWarning("Source for chain #{chain} skipped: name registry is not a contract address", source.Chain);
            else if (!chains.Contains(source.Chain))
                _logger.LogWarning("Source for chain #{chain} skipped: chain is unknown", source.Chain);
            else
                sources.Add(source);
        }

        if (sources.Count == 0)
        {
            _logger.LogWarning("TezosDomainsResolver disabled: no valid sources configured");
            return;
        }

        _tasks = new(sources.Count);
        foreach (var source in sources)
        {
            _logger.LogInformation("Start worker for registry {source}", source);
            _tasks.Add(Task.Run(() => Worker(source, _cts.Token), _cts.Token));
        }

        _logger.LogInformation("TezosDomainsResolver started");
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

        _logger.LogInformation("TezosDomainsResolver stopped");
    }

    async Task Worker(TezosDomainsSourceConfig source, CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(_config.SyncPeriod);
        RegistryInfo? registry = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (registry == null)
                {
                    var resolved = await _store.GetRegistryAsync(source.Chain, source.NameRegistry, ct);
                    if (resolved == null)
                    {
                        _logger.LogWarning("Registry {source} is not indexed yet", source);
                        await Sleep(period, ct);
                        continue;
                    }

                    _logger.LogInformation("Ensure DB indexes for registry {source}", source);
                    await _store.EnsureIndexesAsync(resolved, ct);

                    registry = resolved;

                    _logger.LogInformation("Registry {source} resolved, records: #{records}, expiry: #{expiry}, reverse: #{reverse}",
                        source, registry.RecordsBigMap, registry.ExpiryBigMap, registry.ReverseBigMap);
                }

                await SyncDomains(registry, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync domains from registry {source}", source);
            }

            await Sleep(period, ct);
        }
    }

    async Task SyncDomains(RegistryInfo registry, CancellationToken ct)
    {
        var state = await GetStateAsync(registry, ct);

        while (!ct.IsCancellationRequested)
        {
            await using var conn = await _store.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            var head = await _store.GetHeadAsync(conn, registry.ChainId, ct);
            if (head == null) break;

            var from = state.Level;
            if (state.HeadLevel is int stateHeadLevel && state.HeadHash is byte[] stateHeadHash &&
                !await _store.IsValidBranchAsync(conn, new BlockInfo(registry.ChainId, stateHeadLevel, stateHeadHash), ct))
            {
                var reorgFrom = Math.Max(0, Math.Min(stateHeadLevel - _config.ReorgDepth + 1, head.Level + 1));

                var deleted = await _store.DeleteReorgedDomainsAsync(conn, registry, reorgFrom, ct);
                var invalidated = await _store.GetInvalidatedDomainRecordsAsync(conn, registry, reorgFrom, ct);
                var saved = await _store.SaveDomainsAsync(conn, registry.ChainId, registry.Id, ParseDomains(invalidated), ct);

                _logger.LogWarning("Block {level} on chain #{chain} was reorged, {cnt1} domains deleted, {cnt2} invalidated from level {from}",
                    stateHeadLevel, registry.ChainId, deleted, saved, reorgFrom);

                from = Math.Min(from, reorgFrom);
            }
            else if (from > head.Level)
            {
                break;
            }
            else
            {
                // saved ahead of the commit, so that losing the save after it can't hide a reorg
                state.HeadLevel = head.Level;
                state.HeadHash = head.Hash;
                await SaveStateAsync(registry, state, ct);
            }

            var to = head.Level;
            if (from <= to)
            {
                to = await _store.GetBatchLastLevelAsync(conn, registry, from, to, _config.BatchSize, ct) ?? to;

                var tail = head.Level - _config.ReorgDepth;

                var records = await _store.GetDomainRecordsAsync(conn, registry, from, to, tail, ct);
                var saved = await _store.SaveDomainsAsync(conn, registry.ChainId, registry.Id, ParseDomains(records), ct);
                var expirations = await _store.UpdateExpirationsAsync(conn, registry, from, to, tail, ct);
                var reverses = await _store.UpdateReverseRecordsAsync(conn, registry, from, to, tail, ct);

                if (saved > 0 || expirations > 0 || reverses > 0)
                    _logger.LogDebug("{saved} domain(s) saved, {expirations} expiration(s) and {reverses} reverse record(s) updated at levels {from}-{to}",
                        saved, expirations, reverses, from, to);
            }

            await tx.CommitAsync(ct);

            state.Level = to + 1;
            state.HeadLevel = head.Level;
            state.HeadHash = head.Hash;
            await SaveStateAsync(registry, state, ct);

            if (state.Level > head.Level) break;
        }
    }

    List<DomainInfo> ParseDomains(List<DomainRecord> rows)
    {
        var res = new List<DomainInfo>(rows.Count);

        foreach (var row in rows)
        {
            try
            {
                res.Add(ParseDomain(row));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse domain record #{id}", row.Id);
            }
        }

        return res;
    }

    static DomainInfo ParseDomain(DomainRecord row)
    {
        return new DomainInfo(
            Id: row.Id,
            Level: int.Parse(row.Level ?? throw new FormatException("'level' is missing")),
            Name: ParseName(row.NameHex ?? throw new FormatException("key is not a string")),
            Owner: row.Owner ?? throw new FormatException("'owner' is missing"),
            Address: row.Address,
            Reverse: row.Reverse,
            Expiration: row.Expiration,
            Data: row.Data is string data && data != "{}" ? ParseData(data) : null,
            FirstLevel: row.FirstLevel,
            LastLevel: row.MaxLastLevel);
    }

    static string ParseName(string hex)
    {
        return Utf8.GetString(Hex.GetBytes(hex)).Replace('\0', Regexes.NullEscapeChar);
    }

    static string? ParseData(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        var res = new Dictionary<string, object>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                res[prop.Name] = prop.Value.Clone();
                continue;
            }

            var hex = prop.Value.GetString()!;
            if (!Hex.TryGetBytes(hex, out var bytes))
            {
                res[prop.Name] = hex;
                continue;
            }

            if (TryParseJson(bytes, out var value))
                res[prop.Name] = value;
            else
                res[prop.Name] = IsReadable(bytes) ? Utf8.GetString(bytes) : hex;
        }

        return res.Count == 0
            ? null
            : Regexes.RestrictedUnicode().Replace(JsonSerializer.Serialize(res, WrappedDataJsonOptions), Regexes.NullEscapeString);
    }

    static bool TryParseJson(byte[] bytes, out JsonElement value)
    {
        try
        {
            // valid JSON can still fail to be written back (e.g. a lone surrogate escape)
            value = JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<JsonElement>(bytes, DataJsonOptions), DataJsonOptions);
        }
        catch
        {
            value = default;
            return false;
        }

        // or hold a number out of the numeric range, which jsonb rejects, or numbers that jsonb expands (1e131071 is 131072 digits)
        var growth = 0L;
        return ValidateNumbers(value, ref growth) && growth <= bytes.Length;
    }

    static bool ValidateNumbers(JsonElement value, ref long growth)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                var raw = JsonMarshal.GetRawUtf8Value(value);
                if (!Jsonb.IsValidNumber(raw, out var length))
                    return false;

                growth += length - raw.Length;
                return true;

            case JsonValueKind.Object:
                foreach (var prop in value.EnumerateObject())
                    if (!ValidateNumbers(prop.Value, ref growth))
                        return false;
                return true;

            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                    if (!ValidateNumbers(item, ref growth))
                        return false;
                return true;

            default:
                return true;
        }
    }

    static bool IsReadable(byte[] bytes)
    {
        return bytes.Length > 0 && bytes.Count(x => x >= 32 && x <= 126) / (double)bytes.Length > 0.8;
    }

    async Task<RegistryState> GetStateAsync(RegistryInfo registry, CancellationToken ct)
    {
        var json = await _store.GetStateAsync(registry.ChainId, registry.Hash, ct);
        try { return json != null ? JsonSerializer.Deserialize<RegistryState>(json) ?? new() : new(); }
        catch { return new(); }
    }

    async Task SaveStateAsync(RegistryInfo registry, RegistryState state, CancellationToken ct)
    {
        await _store.SaveStateAsync(registry.ChainId, registry.Hash, JsonSerializer.Serialize(state), ct);
    }

    static async Task Sleep(TimeSpan ts, CancellationToken ct)
    {
        try { await Task.Delay(ts, ct); }
        catch (OperationCanceledException) { }
    }
}

public sealed class RegistryState
{
    /// <summary>
    /// The next level to sync.
    /// </summary>
    public int Level { get; set; }
    public int? HeadLevel { get; set; }
    public byte[]? HeadHash { get; set; }
}
