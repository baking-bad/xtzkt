namespace Xtzkt.Data;

/// <summary>
/// Every entity id carries its chain id in the high bits,
/// so each chain owns a contiguous id range and indexer instances never collide.
/// </summary>
public static class IdLayout
{
    public const int Shift64 = 60;
    public const int Shift32 = 28;
    public const int Shift16 = 12;

    public static long Mask64(int chainId) => (long)chainId << Shift64;
    public static int Mask32(int chainId) => chainId << Shift32;
    public static int Mask16(int chainId) => chainId << Shift16;

    /// <summary>
    /// The lowest int64 id a chain can hold.
    /// </summary>
    public static long MinId64(int chainId) => Mask64(chainId);

    /// <summary>
    /// The highest int64 id a chain can hold.
    /// </summary>
    public static long MaxId64(int chainId) => Mask64(chainId) | ((1L << Shift64) - 1);

    /// <summary>
    /// The lowest int32 id a chain can hold.
    /// </summary>
    public static int MinId32(int chainId) => Mask32(chainId);

    /// <summary>
    /// The highest int32 id a chain can hold.
    /// </summary>
    public static int MaxId32(int chainId) => Mask32(chainId) | ((1 << Shift32) - 1);

    /// <summary>
    /// The lowest int16 id a chain can hold.
    /// </summary>
    public static int MinId16(int chainId) => Mask16(chainId);

    /// <summary>
    /// The highest int16 id a chain can hold.
    /// </summary>
    public static int MaxId16(int chainId) => Mask16(chainId) | ((1 << Shift16) - 1);

    /// <summary>
    /// Inclusive int64 id range of everything after the `fromBlock`, including the block's own id.
    /// </summary>
    public static (long Lo, long Hi) Id64Range(int chainId, long fromBlockId) => (fromBlockId, MaxId64(chainId));
}
