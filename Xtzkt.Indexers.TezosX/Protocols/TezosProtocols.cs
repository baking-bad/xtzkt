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
            services.AddScoped<Proto02Handler>();
            services.AddScoped<Proto03Handler>();
            services.AddScoped<Proto04Handler>();
            services.AddScoped<Proto05Handler>();
            services.AddScoped<Proto08Handler>();
        }

        public static ProtocolHandler GetProtocolHandler(this IServiceProvider services, string? kernel)
        {
            if (GetKernelHandler(services, kernel) is ProtocolHandler handler)
                return handler;

            if (!FallbackToLatestKernel(services))
                throw new NotImplementedException($"Kernel {kernel} is not supported. Set FallbackToLatestKernel=true to index unknown kernels with the latest kernel handler");

            // the handler of the latest kernel; keep in sync with the switch below
            return services.GetRequiredService<Proto08Handler>();
        }

        static ProtocolHandler? GetKernelHandler(IServiceProvider services, string? kernel)
        {
            return kernel switch
            {
                // Genesis
                "0x00213a23a7a34cfbb7c1aba008d2fcad9d6e060882ffeb9745f6e3f039ece5e166" => services.GetRequiredService<Proto01Handler>(),
                "0x00d84727b4afdae430cb694115a672bebfe6cd8c6ccc358298bd29f496c3626519" => services.GetRequiredService<Proto01Handler>(),
                "0x00e58c94bd5a793b09e8b127a1fcf4958c72ca6f86078a78760b41415fb3a7801d" => services.GetRequiredService<Proto01Handler>(),
                "0x00157c3416b2fcf3b3df1e09a2a5f72e293a9037f9cc94a51a0567393e525ced4b" => services.GetRequiredService<Proto01Handler>(),
                // Bifrost
                "0x00fda6968ec17ed11dee02dc91d15606e6f02c8d7e00d8baeaee24fc0188898261" => services.GetRequiredService<Proto02Handler>(),
                // Calypso
                "0x00224058a50dbf4c0b5f6d5e4ee672cd63d0911959b335e587b4112a7eea7b2323" => services.GetRequiredService<Proto03Handler>(),
                // Calypso 2
                "0x008ce0e105f0f1446d78430badcc83aa5672c66bf0bc4fb51962cb765c80e8a60e" => services.GetRequiredService<Proto04Handler>(),
                // Dionysus
                "0x0008105ea6fb0e4331d7bbc93f0e8843ae91eeb235741054cb2b345ac2d19b9ec9" => services.GetRequiredService<Proto05Handler>(),
                // Dionysus R1
                "0x0001010d789e7cccc25c785cf73a658574ed0995ef36b8416a46ab0ddc6b058b39" => services.GetRequiredService<Proto05Handler>(),

                "0x00985fe6f477169765206cfa26dbe7d58b333989d733363c9c648cc2707697df21" => throw new NotImplementedException("Etherlink shadownet isn't yet supported"),
                // Tezos X
                "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" => services.GetRequiredService<Proto08Handler>(), // v0.1
                "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a" => services.GetRequiredService<Proto08Handler>(), // v0.7
                "0x007491e390ec047ffa4edb877c25b41cc46d72884aaa8fa367b952f0c57b85140f" => services.GetRequiredService<Proto08Handler>(), // v0.9
                "0x005f5acc7705865ed1e40561a7c5ceba8b59364a3df39b378c8e4d96350b7135ac" => services.GetRequiredService<Proto08Handler>(), // v0.10
                "0x00b14aa3ca1379bcb6b607cb5917572ecda788d63240727c01dec75ffa4bc75c25" => services.GetRequiredService<Proto08Handler>(), // v0.10
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
