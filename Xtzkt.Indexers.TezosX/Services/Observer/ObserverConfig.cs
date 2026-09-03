namespace Xtzkt.Indexers.TezosX.Services
{
    public class ObserverConfig
    {
        public string Method { get; set; } = "polling";
        public int Period { get; set; } = 1000;
    }

    public static class ObserverConfigExt
    {
        public static ObserverConfig GetObserverConfig(this IConfiguration config)
        {
            return config.GetSection("Observer")?.Get<ObserverConfig>() ?? new();
        }
    }
}
