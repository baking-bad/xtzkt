namespace Xtzkt.Indexers.Common.Extensions;

public static class Int32Extension
{
    public static long MulRatio(this int value, long numerator, long denominator)
    {
        return (long)(long.BigMul(value, numerator) / denominator);
    }
}
