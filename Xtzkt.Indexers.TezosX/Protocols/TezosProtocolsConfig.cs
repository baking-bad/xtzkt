namespace Xtzkt.Indexers.TezosX.Services
{
    public class TezosProtocolsConfig
    {
        public int BatchSize { get; set; } = 256;
        public int PrefetchDepth { get; set; } = 8;
        public bool FallbackToLatestKernel { get; set; } = false;
        public StaticCalls StaticCalls { get; set; } = StaticCalls.StoreFailed;
    }

    public enum StaticCalls
    {
        /// <summary>Store every static call.</summary>
        StoreAll,

        /// <summary> Store only static subtrees whose caller is not applied. </summary>
        StoreFailed,

        /// <summary>Drop every static call.</summary>
        Drop,
    }

    public static class TezosProtocolsConfigExt
    {
        public static TezosProtocolsConfig GetTezosProtocolsConfig(this IConfiguration config)
        {
            var res = config.GetSection("Protocols")?.Get<TezosProtocolsConfig>() ?? new();

            if (res.BatchSize < 1)
                throw new Exception("Protocols.BatchSize must be at least 1");

            if (res.PrefetchDepth < 0)
                throw new Exception("Protocols.PrefetchDepth must not be negative");

            return res;
        }
    }
}
