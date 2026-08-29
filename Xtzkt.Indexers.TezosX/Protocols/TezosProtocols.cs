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
            services.AddScoped<Proto06Handler>();
            services.AddScoped<Proto07Handler>();
            services.AddScoped<Proto08Handler>();
            services.AddScoped<Proto09Handler>();
            services.AddScoped<Proto10Handler>();
        }

        public static ProtocolHandler GetProtocolHandler(this IServiceProvider services, string? kernel)
        {
            if (GetKernelHandler(services, kernel) is ProtocolHandler handler)
                return handler;

            if (!FallbackToLatestKernel(services))
                throw new NotImplementedException($"Kernel {kernel} is not supported. Set FallbackToLatestKernel=true to index unknown kernels with the latest kernel handler");

            // the handler of the latest kernel; keep in sync with the switch below
            return services.GetRequiredService<Proto10Handler>();
        }

        static ProtocolHandler? GetKernelHandler(IServiceProvider services, string? kernel)
        {
            return kernel switch
            {
                // Genesis 0.0
                "0x00213a23a7a34cfbb7c1aba008d2fcad9d6e060882ffeb9745f6e3f039ece5e166" => services.GetRequiredService<Proto01Handler>(),
                // Genesis 0.1
                "0x00d84727b4afdae430cb694115a672bebfe6cd8c6ccc358298bd29f496c3626519" => services.GetRequiredService<Proto01Handler>(),
                // Etherlink 1.0
                "0x00e58c94bd5a793b09e8b127a1fcf4958c72ca6f86078a78760b41415fb3a7801d" => services.GetRequiredService<Proto01Handler>(),
                // Etherlink 1.1
                "0x00157c3416b2fcf3b3df1e09a2a5f72e293a9037f9cc94a51a0567393e525ced4b" => services.GetRequiredService<Proto01Handler>(),
                // Bifrost 2.0
                "0x00fda6968ec17ed11dee02dc91d15606e6f02c8d7e00d8baeaee24fc0188898261" => services.GetRequiredService<Proto02Handler>(),
                // Calypso 3.0
                "0x00224058a50dbf4c0b5f6d5e4ee672cd63d0911959b335e587b4112a7eea7b2323" => services.GetRequiredService<Proto03Handler>(),
                // Calypso 3.1
                "0x008ce0e105f0f1446d78430badcc83aa5672c66bf0bc4fb51962cb765c80e8a60e" => services.GetRequiredService<Proto04Handler>(),
                // Dionysus 4.0
                "0x0008105ea6fb0e4331d7bbc93f0e8843ae91eeb235741054cb2b345ac2d19b9ec9" => services.GetRequiredService<Proto05Handler>(),
                // Dionysus 4.1
                "0x0001010d789e7cccc25c785cf73a658574ed0995ef36b8416a46ab0ddc6b058b39" => services.GetRequiredService<Proto05Handler>(),
                // Ebisu 5.0
                "0x00fea18ffecd0563f942b8b4c67911302754d7e505b5b5672ff03cb927b79ba830" => services.GetRequiredService<Proto06Handler>(),
                // Farfadet 6.0
                "0x0079e0f348b608ce486c9e5e1fdf84b650019922bf3383b562522c2c8f60a098da" => services.GetRequiredService<Proto07Handler>(),
                // Farfadet 6.1
                "0x0056aea7f98b2bc4d18edb450b2f098f6e95e5356f30a1fac2b50080f3e482bad1" => services.GetRequiredService<Proto08Handler>(),
                // Farfadet 6.2
                "0x00a932181ea0b3446ec1d509c33680a473f133bd1aa92d144d2011fe9fd1e2787f" => services.GetRequiredService<Proto09Handler>(),
                // Farfadet 6.3
                "0x0005d2c53f57df68b2027ecf592169cf8ce0ee7b3a6ecc215d58e42733c6eed131" => services.GetRequiredService<Proto09Handler>(),
                // Farfadet 6.4
                "0x00625d22abf10a520cae5489b7e19df70219a150d336ee6dc0a8eb4c21eca43c1b" => services.GetRequiredService<Proto09Handler>(),
                // Farfadet 6.5
                "0x007c73209bc68c2e0099e105b92ef4c674387532afbf5d51b7f1043472f9d65e9b" => services.GetRequiredService<Proto09Handler>(),
                // Farfadet 6.6
                "0x0083d8142e9c5f2a35ead6eb31d6344f3803f90eacb03ccfb6c482df353f85908a" => services.GetRequiredService<Proto09Handler>(),
                // Tezos X 0.10-shadownet
                "0x008c903318dfc0016de771f981069498f7774f3c35ffcc3f2dce63f5a3b6d03df6" => services.GetRequiredService<Proto10Handler>(),

                // Tezos X
                "0x00a237d1781d29dbcc7b9621684831f7c553946aca808acaf404d6818dc39b18e3" => services.GetRequiredService<Proto10Handler>(), // v0.1
                "0x007a6ac98660fa68cab09abfb3a59be93ccf4a5d47aeb44a00ffb0a3babdba448a" => services.GetRequiredService<Proto10Handler>(), // v0.7
                "0x007491e390ec047ffa4edb877c25b41cc46d72884aaa8fa367b952f0c57b85140f" => services.GetRequiredService<Proto10Handler>(), // v0.9
                "0x005f5acc7705865ed1e40561a7c5ceba8b59364a3df39b378c8e4d96350b7135ac" => services.GetRequiredService<Proto10Handler>(), // v0.10
                "0x00b14aa3ca1379bcb6b607cb5917572ecda788d63240727c01dec75ffa4bc75c25" => services.GetRequiredService<Proto10Handler>(), // v0.10
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
