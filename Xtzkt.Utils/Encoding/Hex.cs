using System.Diagnostics.CodeAnalysis;

namespace Xtzkt.Utils.Encoding;

public static class Hex
{
    static ReadOnlySpan<byte> HexAscii =>
    [
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 0,   1,
        2,   3,   4,   5,   6,   7,   8,   9,   255, 255,
        255, 255, 255, 255, 255, 10,  11,  12,  13,  14,
        15,  255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
        255, 255, 255, 255, 255, 255, 255, 10,  11,  12,
        13,  14,  15
    ];

    /// <summary>
    /// Returns hex string with 0x prefix.
    /// </summary>
    /// <param name="bytes">Bytes to convert.</param>
    /// <returns></returns>
    public static string GetString(ReadOnlySpan<byte> bytes)
    {
        var buffer = new char[2 + (bytes.Length << 1)];
        buffer[0] = '0';
        buffer[1] = 'x';
        Convert.TryToHexStringLower(bytes, buffer.AsSpan(2), out _);
        return new string(buffer);
    }

    /// <summary>
    /// Returns hex string without 0x prefix.
    /// </summary>
    /// <param name="bytes">Bytes to convert</param>
    /// <returns></returns>
    public static string GetStringRaw(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Parses hex string into byte array.
    /// </summary>
    /// <param name="hex">Hex string to parse.</param>
    /// <returns></returns>
    /// <exception cref="FormatException">Throws when input contains invalid hex symbols</exception>
    public static byte[] GetBytes(string hex)
    {
        var pos = (hex.Length >= 2 && hex[0] == '0' && (hex[1] == 'x' || hex[1] == 'X')) ? 2 : 0;

        if (hex.Length == pos)
            return [];

        if ((hex.Length & 0x1) == 0)
        {
            var res = new byte[(hex.Length - pos) >> 1];
            for (int j = 0; j < res.Length; j++)
            {
                if (hex[pos] > 102) throw new FormatException("Invalid hex string");
                int h = HexAscii[hex[pos++]];

                if (hex[pos] > 102) throw new FormatException("Invalid hex string");
                int l = HexAscii[hex[pos++]];

                if ((h | l) == 255) throw new FormatException("Invalid hex string");
                res[j] = (byte)((h << 4) + l);
            }
            return res;
        }
        else
        {
            var res = new byte[(hex.Length - pos + 1) >> 1];

            if (hex[pos] > 102) throw new FormatException("Invalid hex string");
            int p = HexAscii[hex[pos++]];

            if (p == 255) throw new FormatException("Invalid hex string");
            res[0] = (byte)p;

            for (int j = 1; j < res.Length; j++)
            {
                if (hex[pos] > 102) throw new FormatException("Invalid hex string");
                int h = HexAscii[hex[pos++]];

                if (hex[pos] > 102) throw new FormatException("Invalid hex string");
                int l = HexAscii[hex[pos++]];

                if ((h | l) == 255) throw new FormatException("Invalid hex string");
                res[j] = (byte)((h << 4) + l);
            }
            return res;
        }
    }

    /// <summary>
    /// Tries to parse hex string into byte array.
    /// </summary>
    /// <param name="hex">Hex string to parse.</param>
    /// <param name="bytes">Byte array.</param>
    /// <returns></returns>
    public static bool TryGetBytes(string? hex, [NotNullWhen(true)] out byte[]? bytes)
    {
        if (hex == null)
        {
            bytes = null;
            return false;
        }

        var pos = (hex.Length >= 2 && hex[0] == '0' && (hex[1] == 'x' || hex[1] == 'X')) ? 2 : 0;

        if (hex.Length == pos)
        {
            bytes = [];
            return true;
        }

        if ((hex.Length & 0x1) == 0)
        {
            bytes = new byte[(hex.Length - pos) >> 1];
            for (int j = 0; j < bytes.Length; j++)
            {
                if (hex[pos] > 102) return false;
                int h = HexAscii[hex[pos++]];

                if (hex[pos] > 102) return false;
                int l = HexAscii[hex[pos++]];

                if ((h | l) == 255) return false;
                bytes[j] = (byte)((h << 4) + l);
            }
            return true;
        }
        else
        {
            bytes = new byte[(hex.Length - pos + 1) >> 1];

            if (hex[pos] > 102) return false;
            int p = HexAscii[hex[pos++]];

            if (p == 255) return false;
            bytes[0] = (byte)p;

            for (int j = 1; j < bytes.Length; j++)
            {
                if (hex[pos] > 102) return false;
                int h = HexAscii[hex[pos++]];

                if (hex[pos] > 102) return false;
                int l = HexAscii[hex[pos++]];

                if ((h | l) == 255) return false;
                bytes[j] = (byte)((h << 4) + l);
            }
            return true;
        }
    }
}
