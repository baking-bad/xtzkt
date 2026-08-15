namespace Xtzkt.Services.Metadata.Resolvers.DipDup;

public sealed class DipDupResolverConfig
{
    public bool Enabled { get; set; } = false;
    public int SyncPeriod { get; set; } = 5;
    public int BackfillLimit { get; set; } = 100;

    public int[] RetryDelays { get; set; } = [];
    public DipDupSourceConfig[] Sources { get; set; } = [];
}

public sealed class DipDupSourceConfig
{
    public int Chain { get; set; } = 0;
    public string Url { get; set; } = "https://metadata.dipdup.net/v1/graphql";
    public string? TokenMetadataTable { get; set; } = "token_metadata";
    public string? ContractMetadataTable { get; set; } = "contract_metadata";
    public string HeadStatusTable { get; set; } = "dipdup_head_status";
    public string IndexName { get; set; } = "metadata_mainnet";
    public string Network { get; set; } = "mainnet";
    public int QueryLimit { get; set; } = 10_000;
    public int Timeout { get; set; } = 60;
    public int MaxRps { get; set; } = 10;
    public DipDupFilter? Filter { get; set; }
}

public sealed class DipDupFilter
{
    public FilterMode Mode { get; set; } = FilterMode.Exclude;
    public HashSet<string> Contracts { get; set; } = [];

    public enum FilterMode
    {
        Exclude,
        Include,
    }
}

public static class DipDupResolverConfigExt
{
    public static DipDupResolverConfig GetDipDupResolverConfig(this IConfiguration config)
    {
        return config.GetSection("DipDupResolver").Get<DipDupResolverConfig>() ?? new();
    }
}
