namespace Xtzkt.Api.Services.Cache
{
    public class CacheConfig
    {
        public CacheLimits Address { get; set; } = new(16_000, 64_000);
    }

    public static class CacheConfigExt
    {
        public static CacheConfig GetCacheConfig(this IConfiguration config)
        {
            return config.GetSection("Cache")?.Get<CacheConfig>() ?? new();
        }
    }

    public class CacheLimits(decimal softLimit, decimal hardLimit)
    {
        public decimal SoftLimit { get; init; } = softLimit;
        public decimal HardLimit { get; init; } = hardLimit;

        public int GetHardLimit(int total) => GetLimit(total, HardLimit);

        public int GetSoftLimit(int total) => GetLimit(total, SoftLimit);

        static int GetLimit(int total, decimal limit)
        {
            if (limit == 0) return 0;
            if (limit <= 1) return (int)(total * limit);
            return checked((int)limit);
        }
    }
}
