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
}
