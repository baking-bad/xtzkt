using System.Diagnostics.CodeAnalysis;

namespace Xtzkt.Indexers.TezosX.Utils
{
    public static class Hex
    {
        private static readonly string AsciiHex = "0123456789abcdef";
        private static readonly int[] HexAscii =
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
            13,  14,  15,  255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255
        ];

        public static string Convert(ReadOnlySpan<byte> bytes)
        {
            var array = new char[2 + (bytes.Length << 1)];
            array[0] = '0';
            array[1] = 'x';
            
            for (int pos = 2, i = 0; i < bytes.Length; i++)
            {
                array[pos++] = AsciiHex[bytes[i] >> 4];
                array[pos++] = AsciiHex[bytes[i] & 0xF];
            }

            return new string(array);
        }

        public static byte[] Parse(string hex)
        {
            if ((hex.Length & 0x1) != 0)
                throw new FormatException("Invalid hex string length");

            if (hex.Length == 0)
                return [];

            var i = (hex[1] == 'x' && hex[0] == '0') ? 2 : 0;
            var res = new byte[(hex.Length - i) >> 1];
            for (int j = 0; j < res.Length; j++, i+= 2)
            {
                var h = HexAscii[hex[i]];
                var l = HexAscii[hex[i + 1]];
                if ((h | l) == 255)
                    throw new FormatException("Invalid hex string");
                res[j] = (byte)((h << 4) + l);
            }

            return res;
        }

        public static bool TryParse(string? hex, [NotNullWhen(true)] out byte[]? bytes)
        {
            bytes = null;
            if (hex == null)
                return false;

            if ((hex.Length & 0x1) != 0)
                return false;

            if (hex.Length == 0)
            {
                bytes = [];
                return true;
            }

            var i = (hex[1] == 'x' && hex[0] == '0') ? 2 : 0;
            bytes = new byte[(hex.Length - i) >> 1];
            for (int j = 0; j < bytes.Length; j++, i += 2)
            {
                var h = HexAscii[hex[i]];
                var l = HexAscii[hex[i + 1]];
                if ((h | l) == 255)
                    return false;
                bytes[j] = (byte)((h << 4) + l);
            }

            return true;
        }
    }
}
