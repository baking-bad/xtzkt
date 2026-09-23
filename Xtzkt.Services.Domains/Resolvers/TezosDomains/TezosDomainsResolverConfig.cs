namespace Xtzkt.Services.Domains.Resolvers.TezosDomains;

public sealed class TezosDomainsResolverConfig
{
    public bool Enabled { get; set; } = true;
    public int SyncPeriod { get; set; } = 30;
    public int BatchSize { get; set; } = 10_000;
    public int ReorgDepth { get; set; } = 10;

    public TezosDomainsSourceConfig[] Sources { get; set; } = [];
}

public sealed class TezosDomainsSourceConfig
{
    public int Chain { get; set; } = 0;
    public string NameRegistry { get; set; } = "KT1GBZmSxmnKJXGMdMLbugPfLyUPmuLSMwKS";

    public override string ToString() => $"{Chain}@{NameRegistry}";
}

public static class TezosDomainsResolverConfigExt
{
    public static TezosDomainsResolverConfig GetTezosDomainsResolverConfig(this IConfiguration config)
    {
        return config.GetSection("TezosDomainsResolver").Get<TezosDomainsResolverConfig>() ?? new();
    }
}
