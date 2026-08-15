namespace Xtzkt.Indexers.TezosX.Services
{
    public class TezosProtocolsConfig
    {
        public List<string>? Precompiles { get; set; } = null;
    }

    public static class TezosProtocolsConfigExt
    {
        public static TezosProtocolsConfig GetTezosProtocolsConfig(this IConfiguration config)
        {
            return config.GetSection("Protocols")?.Get<TezosProtocolsConfig>() ?? new();
        }
    }
}
