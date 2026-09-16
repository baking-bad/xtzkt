namespace Xtzkt.Utils.Extensions;
public static class DateTimeExtension
{
    public static DateTime UtcMinValue => DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    public static DateTime UtcMaxValue => DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
    public static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    public static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    public static DateTime TrimMilliseconds(this DateTime value)
    {
        return value.AddTicks(-(value.Ticks % 10_000_000)); ;
    }
}
