namespace Xtzkt.Indexers.TezosX.Services;

public class EvmNodeConfig
{
    public string Endpoint { get; set; } = "https://rpc.tzkt.io/mainnet";
    public int Timeout { get; set; } = 60;
}

public static class EvmNodeConfigExt
{
    public static EvmNodeConfig GetEvmNodeConfig(this IConfiguration config)
    {
        return config.GetSection("EvmNode")?.Get<EvmNodeConfig>() ?? new();
    }
}
