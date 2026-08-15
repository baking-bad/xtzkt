namespace Xtzkt.Api.Services.Database;

public class DbInitConfig
{
    public string? Script { get; set; } = "init.pgsql";
}

public static class DbInitConfigExt
{
    public static DbInitConfig GetDbInitConfig(this IConfiguration config)
    {
        return config.GetSection("DbInit")?.Get<DbInitConfig>() ?? new();
    }
}
