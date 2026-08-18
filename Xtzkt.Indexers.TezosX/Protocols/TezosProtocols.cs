using Xtzkt.Indexers.TezosX.Protocols;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX
{
    public static class TezosProtocols
    {
        static bool? Fallback = null;

        public static void AddTezosProtocols(this IServiceCollection services)
        {
            services.AddScoped<Proto01Handler>();
        }

        public static ProtocolHandler GetProtocolHandler(this IServiceProvider services, string? kernel)
        {
            if (GetKernelHandler(services, kernel) is ProtocolHandler handler)
                return handler;

            if (!FallbackToLatestKernel(services))
                throw new NotImplementedException($"Kernel {kernel} is not supported. Set FallbackToLatestKernel=true to index unknown kernels with the latest kernel handler");

            // the handler of the latest kernel; keep in sync with the switch below
            return services.GetRequiredService<Proto01Handler>();
        }

        static ProtocolHandler? GetKernelHandler(IServiceProvider services, string? kernel)
        {
            return kernel switch
            {
                "0x00213a23a7a34cfbb7c1aba008d2fcad9d6e060882ffeb9745f6e3f039ece5e166" => throw new NotImplementedException("Etherlink mainnet isn't yet supported"),
                "0x00985fe6f477169765206cfa26dbe7d58b333989d733363c9c648cc2707697df21" => throw new NotImplementedException("Etherlink shadownet isn't yet supported"),
                "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" => services.GetRequiredService<Proto01Handler>(), // v0.1
                "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a" => services.GetRequiredService<Proto01Handler>(), // v0.7
                "0x007491e390ec047ffa4edb877c25b41cc46d72884aaa8fa367b952f0c57b85140f" => services.GetRequiredService<Proto01Handler>(), // v0.9
                "0x005f5acc7705865ed1e40561a7c5ceba8b59364a3df39b378c8e4d96350b7135ac" => services.GetRequiredService<Proto01Handler>(), // v0.10
                "0x00b14aa3ca1379bcb6b607cb5917572ecda788d63240727c01dec75ffa4bc75c25" => services.GetRequiredService<Proto01Handler>(), // v0.10
                _ => null
            };
        }

        static bool FallbackToLatestKernel(IServiceProvider services)
        {
            return Fallback ??= services.GetRequiredService<IConfiguration>()
                .GetTezosProtocolsConfig()
                .FallbackToLatestKernel;
        }
    }
}
