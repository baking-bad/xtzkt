using Microsoft.Extensions.Configuration;

namespace Xtzkt.Indexers.Common.Extensions;

public static class ConfigurationExtension
{
    public static string GetDefaultConnectionString(this IConfiguration config)
    {
        return config.GetConnectionString("DefaultConnection") ?? throw new Exception("ConnectionString is missed");
    }
}
