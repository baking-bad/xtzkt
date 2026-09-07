namespace Xtzkt.Api;

public static class ApiConfig
{
    /// <summary>
    /// Max number of values in list filters (`.in`, `.ni`).
    /// </summary>
    public static int MaxBatchSize { get; private set; } = 50;

    /// <summary>
    /// An address with more rows than `limit + FlatteningThreshold` in the queried table is considered "heavy"
    /// and will be queried in a separate UNION ALL branch, rather than within "= ANY (...)".
    /// </summary>
    public static int FlatteningThreshold { get; private set; } = 500;

    /// <summary>
    /// Max number of UNION ALL branches added after flattening.
    /// </summary>
    public static int FlatteningLimit { get; private set; } = 20;

    public static void Init(IConfiguration config)
    {
        var section = config.GetSection("Api");

        MaxBatchSize = ReadPositive(section, nameof(MaxBatchSize), MaxBatchSize);
        FlatteningThreshold = ReadPositive(section, nameof(FlatteningThreshold), FlatteningThreshold);
        FlatteningLimit = ReadPositive(section, nameof(FlatteningLimit), FlatteningLimit);
    }

    static int ReadPositive(IConfigurationSection section, string key, int fallback)
    {
        var value = section.GetValue(key, fallback);
        return value >= 1 ? value : throw new InvalidOperationException($"Api:{key} must be positive");
    }
}
