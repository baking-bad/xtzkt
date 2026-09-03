namespace Xtzkt.Indexers.TezosX.Services;

public class EvmNodeConfig
{
    public string Endpoint { get; set; } = "https://rpc.tzkt.io/mainnet";
    public string? WsEndpoint { get; set; } = null;
    public int Timeout { get; set; } = 60;

    public string GetWsEndpoint()
    {
        if (!string.IsNullOrEmpty(WsEndpoint))
            return WsEndpoint;

        var uri = new Uri(Endpoint, UriKind.Absolute);
        var scheme = uri.Scheme switch
        {
            "http" or "ws" => "ws",
            "https" or "wss" => "wss",
            _ => throw new Exception($"Unsupported EvmNode endpoint scheme: {uri.Scheme}")
        };

        return $"{scheme}://{uri.Host}{(uri.IsDefaultPort ? string.Empty : $":{uri.Port}")}{uri.AbsolutePath.TrimEnd('/')}/ws";
    }
}

public static class EvmNodeConfigExt
{
    public static EvmNodeConfig GetEvmNodeConfig(this IConfiguration config)
    {
        return config.GetSection("EvmNode")?.Get<EvmNodeConfig>() ?? new();
    }
}
