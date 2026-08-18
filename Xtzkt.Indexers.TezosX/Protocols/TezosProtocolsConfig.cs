namespace Xtzkt.Indexers.TezosX.Services
{
    public class TezosProtocolsConfig
    {
        public bool FallbackToLatestKernel { get; set; } = false;
    }

    public static class TezosProtocolsConfigExt
    {
        public static TezosProtocolsConfig GetTezosProtocolsConfig(this IConfiguration config)
        {
            return config.GetSection("Protocols")?.Get<TezosProtocolsConfig>() ?? new();
        }
    }
}
