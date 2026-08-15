using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Utils.Network;

namespace Xtzkt.Services.Metadata.Resolvers.DipDup;

sealed class DipDupClient(DipDupSourceConfig config) : IDisposable
{
    public string Url => _config.Url;
    public int QueryLimit => _config.QueryLimit;
    public bool SyncTokenMetadata => _config.TokenMetadataTable != null;
    public bool SyncContractMetadata => _config.ContractMetadataTable != null;

    readonly DipDupSourceConfig _config = config;
    readonly TzktClient _client = new(config.Timeout);
    readonly RateLimiter _rps = new(config.MaxRps);

    public async Task<long> GetSentinelAsync(CancellationToken ct)
    {
        var query = $$"""
            query {
                items: {{_config.HeadStatusTable}}(
                    where: {
                        index_name: { _eq: "{{_config.IndexName}}" }
                    },
                    order_by: {
                        created_at: asc
                    },
                    limit: 1
                ) {
                    created_at
                }
            }
            """;

        var response = await Post<DipDupHeadStatus>(query, ct);
        return response.Data.Items.FirstOrDefault()?.CreatedAt ?? 0;
    }

    public async Task<List<DipDupTokenMetadata>> GetTokenMetadataAsync(long lastUpdateId, CancellationToken ct)
    {
        var query = $$"""
            query {
                items: {{_config.TokenMetadataTable}}(
                    where: {
                        network: { _eq: "{{_config.Network}}" },
                        update_id: { _gt: "{{lastUpdateId}}" },
                        status: { _gt: 1 },
                        {{ContractFilter()}}
                    },
                    order_by: {
                        update_id: asc
                    },
                    limit: {{_config.QueryLimit}}
                ) {
                    contract
                    link
                    metadata
                    status
                    token_id
                    update_id
                    updated_at
                }
            }
            """;

        var response = await Post<DipDupTokenMetadata>(query, ct);
        return response.Data.Items;
    }

    public async Task<List<DipDupContractMetadata>> GetContractMetadataAsync(long lastUpdateId, CancellationToken ct)
    {
        var query = $$"""
            query {
                items: {{_config.ContractMetadataTable}}(
                    where: {
                        network: { _eq: "{{_config.Network}}" },
                        update_id: { _gt: "{{lastUpdateId}}" },
                        {{ContractFilter()}}
                    },
                    order_by: {
                        update_id: asc
                    },
                    limit: {{_config.QueryLimit}}
                ) {
                    contract
                    metadata
                    update_id
                }
            }
            """;

        var response = await Post<DipDupContractMetadata>(query, ct);
        return response.Data.Items;
    }

    string ContractFilter()
    {
        if (_config.Filter is not DipDupFilter filter || filter.Contracts.Count == 0)
            return string.Empty;

        var mode = filter.Mode == DipDupFilter.FilterMode.Include ? "_in" : "_nin";
        var list = string.Join(", ", filter.Contracts.Select(x => $"\"{x}\""));
        return $"contract: {{ {mode}: [ {list} ] }}";
    }

    public async Task<List<DipDupTokenMetadata>> GetTokenMetadataAsync(IReadOnlyCollection<(string Contract, string TokenId)> tokens, CancellationToken ct)
    {
        if (_config.Filter is DipDupFilter filter)
            tokens = filter.Mode == DipDupFilter.FilterMode.Include
                ? [.. tokens.Where(k => filter.Contracts.Contains(k.Contract))]
                : [.. tokens.Where(k => !filter.Contracts.Contains(k.Contract))];

        if (tokens.Count == 0)
            return [];

        var contracts = string.Join(", ", tokens.Select(x => $"\"{x.Contract}\"").Distinct());
        var tokenIds = string.Join(", ", tokens.Select(x => $"\"{x.TokenId}\"").Distinct());
        var wanted = tokens.ToHashSet();

        var items = new List<DipDupTokenMetadata>(tokens.Count);
        var lastUpdateId = -1L;
        while (!ct.IsCancellationRequested)
        {
            var response = await Post<DipDupTokenMetadata>($$"""
                query {
                    items: {{_config.TokenMetadataTable}}(
                        where: {
                            network: { _eq: "{{_config.Network}}" },
                            update_id: { _gt: "{{lastUpdateId}}" },
                            contract: { _in: [ {{contracts}} ] },
                            token_id: { _in: [ {{tokenIds}} ] },
                            status: { _gt: 1 }
                        },
                        order_by: {
                            update_id: asc
                        },
                        limit: {{_config.QueryLimit}}
                    ) {
                        contract
                        link
                        metadata
                        status
                        token_id
                        update_id
                        updated_at
                    }
                }
                """, ct);

            items.AddRange(response.Data.Items.Where(x => wanted.Contains((x.Contract, x.TokenId))));
            if (response.Data.Items.Count < _config.QueryLimit) break;
            lastUpdateId = response.Data.Items[^1].UpdateId;
        }

        return items;
    }

    async Task<DipDupResponse<T>> Post<T>(string query, CancellationToken ct)
    {
        await _rps.AcquireAsync(ct);

        var payload = JsonSerializer.Serialize(new { query, variables = (object?)null });
        return await _client.PostAsync<DipDupResponse<T>>(_config.Url, payload, ct);
    }

    public void Dispose() => _client.Dispose();
}

sealed class DipDupResponse<T>
{
    [JsonPropertyName("data")]
    public required DipDupData<T> Data { get; set; }
}

sealed class DipDupData<T>
{
    [JsonPropertyName("items")]
    public required List<T> Items { get; set; }
}

sealed class DipDupHeadStatus
{
    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; set; }
}

sealed class DipDupTokenMetadata
{
    [JsonPropertyName("contract")]
    public required string Contract { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }

    [JsonPropertyName("status")]
    public required int Status { get; set; }

    [JsonPropertyName("token_id")]
    public required string TokenId { get; set; }

    [JsonPropertyName("update_id")]
    public required long UpdateId { get; set; }

    [JsonPropertyName("updated_at")]
    public required long UpdatedAt { get; set; }
}

sealed class DipDupContractMetadata
{
    [JsonPropertyName("contract")]
    public required string Contract { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }

    [JsonPropertyName("update_id")]
    public required long UpdateId { get; set; }
}
