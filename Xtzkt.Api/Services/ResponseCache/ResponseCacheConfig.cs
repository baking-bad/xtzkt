namespace Xtzkt.Api.Services.ResponseCache;

public class ResponseCacheConfig
{
    public int CacheSize { get; set; } = 256;
}

public static class ResponseCacheConfigExt
{
    public static ResponseCacheConfig GetResponseCacheConfig(this IConfiguration config)
    {
        return config.GetSection("ResponseCache")?.Get<ResponseCacheConfig>() ?? new();
    }
}
