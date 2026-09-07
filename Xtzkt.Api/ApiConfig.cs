namespace Xtzkt.Api;

public static class ApiConfig
{
    /// <summary>
    /// `limit` applied when the request doesn't specify one. Every `Max*Limit` must be at least this much.
    /// </summary>
    public const int DefaultLimit = 100;

    /// <summary>
    /// Max number of values in list filters (`.in`, `.ni`).
    /// </summary>
    public static int MaxBatchSize { get; private set; } = 50;

    /// <summary>
    /// Max `limit` on list endpoints.
    /// </summary>
    public static int MaxLimit { get; private set; } = 10_000;

    /// <summary>
    /// Max `limit` on activity endpoints, which fan out to up to a dozen tables per request and merge
    /// the pages in memory, so their cost is `tables * limit`.
    /// </summary>
    public static int MaxActivityLimit { get; private set; } = 1_000;

    /// <summary>
    /// Max `offset`: the DB materializes and discards skipped rows, so an offset costs time proportional
    /// to its value; deeper paging goes through `cursor`. Zero disables offset paging entirely.
    /// </summary>
    public static int MaxOffset { get; private set; } = 100_000;

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

        MaxBatchSize = Read(section, nameof(MaxBatchSize), MaxBatchSize);
        MaxLimit = Read(section, nameof(MaxLimit), MaxLimit, min: DefaultLimit);
        MaxActivityLimit = Read(section, nameof(MaxActivityLimit), MaxActivityLimit, min: DefaultLimit);
        MaxOffset = Read(section, nameof(MaxOffset), MaxOffset, min: 0);
        FlatteningThreshold = Read(section, nameof(FlatteningThreshold), FlatteningThreshold);
        FlatteningLimit = Read(section, nameof(FlatteningLimit), FlatteningLimit);
    }

    static int Read(IConfigurationSection section, string key, int fallback, int min = 1)
    {
        var value = section.GetValue(key, fallback);
        return value >= min ? value : throw new InvalidOperationException($"Api:{key} must be at least {min}");
    }
}
