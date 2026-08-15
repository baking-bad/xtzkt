namespace Xtzkt.Services.Metadata.Resolvers.Ipfs;

public sealed class IpfsResolverConfig
{
    public bool Enabled { get; set; } = true;
    public int SyncPeriod { get; set; } = 5;
    public int MaxQueue { get; set; } = 1024;
    public int SaveBatch { get; set; } = 20;

    public int[] RetryDelays { get; set; } = [];
    public int[] RetryTimeouts { get; set; } = [];
    public IpfsGatewayConfig[] IpfsGateways { get; set; } = [];
}

public sealed class IpfsGatewayConfig
{
    public required string Url { get; set; }
    public int MaxRps { get; set; } = 10;
    public int Timeout { get; set; } = 10;
    public int Workers { get; set; } = 1;
}

public static class IpfsResolverConfigExt
{
    public static IpfsResolverConfig GetIpfsResolverConfig(this IConfiguration config)
    {
        return config.GetSection("IpfsResolver").Get<IpfsResolverConfig>() ?? new();
    }
}