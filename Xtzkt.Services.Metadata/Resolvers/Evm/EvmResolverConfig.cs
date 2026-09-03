namespace Xtzkt.Services.Metadata.Resolvers.Evm;

public sealed class EvmResolverConfig
{
    public bool Enabled { get; set; } = true;
    public int SyncPeriod { get; set; } = 5;
    public int MaxQueue { get; set; } = 1024;

    public int[] RetryDelays { get; set; } = [];
    public int[] RetryTimeouts { get; set; } = [];
    public EvmNodeConfig[] EvmNodes { get; set; } = [];
}

public sealed class EvmNodeConfig
{
    public required string Url { get; set; }

    public int MaxBatchSize { get; set; } = 60;
    public int MaxRps { get; set; } = 10;
    public int Timeout { get; set; } = 10;

    public string? ApiKeyHeader { get; set; }
    public string? ApiKey { get; set; }
}

public static class EvmResolverConfigExt
{
    public static EvmResolverConfig GetEvmResolverConfig(this IConfiguration config)
    {
        return config.GetSection("EvmResolver").Get<EvmResolverConfig>() ?? new();
    }
}