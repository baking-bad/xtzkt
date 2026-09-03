using System.Diagnostics.CodeAnalysis;

namespace Xtzkt.Utils.Encoding;

public static class Utf8
{
    /// <summary>
    /// Returns UTF8 string.
    /// </summary>
    /// <param name="bytes">Bytes to convert.</param>
    /// <returns></returns>
    public static string GetString(ReadOnlySpan<byte> bytes)
    {
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Parses UTF8 string into byte array.
    /// </summary>
    /// <param name="utf8">UTF8 string to parse.</param>
    /// <returns></returns>
    public static byte[] GetBytes(string utf8)
    {
        return System.Text.Encoding.UTF8.GetBytes(utf8);
    }

    /// <summary>
    /// Tries to parse UTF8 string into byte array.
    /// </summary>
    /// <param name="utf8">UTF8 string to parse.</param>
    /// <param name="bytes">Byte array.</param>
    /// <returns></returns>
    public static bool TryGetBytes(string utf8, [NotNullWhen(true)] out byte[]? bytes)
    {
        try
        {
            bytes = System.Text.Encoding.UTF8.GetBytes(utf8);
            return true;
        }
        catch
        {
            bytes = null;
            return false;
        }
    }
}
