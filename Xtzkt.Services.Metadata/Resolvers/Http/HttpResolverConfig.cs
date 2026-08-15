namespace Xtzkt.Services.Metadata.Resolvers.Http;

public sealed class HttpResolverConfig
{
    public bool Enabled { get; set; } = true;
    public int SyncPeriod { get; set; } = 5;
    public int MaxQueue { get; set; } = 1024;
    public int SaveBatch { get; set; } = 20;

    public int[] RetryDelays { get; set; } = [];
    public int[] RetryTimeouts { get; set; } = [];
    public int MaxRps { get; set; } = 10;
    public int Timeout { get; set; } = 10;
    public int Workers { get; set; } = 1;
}

public static class HttpResolverConfigExt
{
    public static HttpResolverConfig GetHttpResolverConfig(this IConfiguration config)
    {
        return config.GetSection("HttpResolver").Get<HttpResolverConfig>() ?? new();
    }
}