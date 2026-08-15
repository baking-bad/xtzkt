using Xtzkt.Indexers.TezosX.Protocols;

namespace Xtzkt.Indexers.TezosX
{
    public static class TezosProtocols
    {
        public static void AddTezosProtocols(this IServiceCollection services)
        {
            services.AddScoped<Proto01Handler>();
        }

        public static ProtocolHandler GetProtocolHandler(this IServiceProvider services, string? kernel)
        {
            return kernel switch
            {
                "0x00213a23a7a34cfbb7c1aba008d2fcad9d6e060882ffeb9745f6e3f039ece5e166" => throw new NotImplementedException("Etherlink mainnet isn't yet supported"),
                "0x00985fe6f477169765206cfa26dbe7d58b333989d733363c9c648cc2707697df21" => throw new NotImplementedException("Etherlink shadownet isn't yet supported"),
                "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" => services.GetRequiredService<Proto01Handler>(), // v0.1
                "0x0022c4ddbe3724a2cd7bf459ce4e5a0589c875dac084a87efd4d8a40d482aabf2f" => services.GetRequiredService<Proto01Handler>(), // v0.2
                "0x0030c2931834b9012caeef0b9fd815e48972c37e8c621ac2790e40eaf0a1fddeff" => services.GetRequiredService<Proto01Handler>(), // v0.3
                "0x0074d86dab68edcf623c10fad1f12b28622f08af39917f1d2d2997bc27101bda42" => services.GetRequiredService<Proto01Handler>(), // v0.4
                "0x00101bd944756199ad19c06fcc12743a90e0db795118ff7479b46c4e80451f6931" => services.GetRequiredService<Proto01Handler>(), // v0.5
                "0x0019ea6db27c8e9f9081aecc112c01614505d3fc7eaa1a50e9822a6143c348eff7" => services.GetRequiredService<Proto01Handler>(), // v0.6
                "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a" => services.GetRequiredService<Proto01Handler>(), // v0.7
                "0x007491e390ec047ffa4edb877c25b41cc46d72884aaa8fa367b952f0c57b85140f" => services.GetRequiredService<Proto01Handler>(), // v0.7
                _ => throw new NotImplementedException($"Kernel {kernel} is not supported")
            };
        }
    }
}
