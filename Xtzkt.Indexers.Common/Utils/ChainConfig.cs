using Microsoft.Extensions.Configuration;

namespace Xtzkt.Indexers.Common.Utils;

public class ChainConfig
{
    public required int Id { get; set; }
    public string? Network { get; set; }
    public int? MichelsonActivationLevel { get; set; }
}

public static class ChainConfigExt
{
    public static ChainConfig GetChainConfig(this IConfiguration config)
    {
        return config.GetSection("Chain").Get<ChainConfig>() ?? throw new Exception("Chain config is missed");
    }
}
