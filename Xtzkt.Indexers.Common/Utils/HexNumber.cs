using System.Numerics;

namespace Xtzkt.Indexers.Common.Utils
{
    static class HexNumber
    {
        private static readonly int[] HexAscii =
        [
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            0,   1,   2,   3,   4,   5,   6,   7,   8,   9,   255, 255, 255, 255, 255, 255,
            255, 10,  11,  12,  13,  14,  15,  255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 10,  11,  12,  13,  14,  15,  255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255
        ];

        public static int GetInt32Reverse(ReadOnlySpan<byte> bytes)
        {
            var cnt = bytes.Length;
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                if (bytes[i] == 0) cnt--;
                else break;
            }

            if (cnt > 4)
                throw new Exception($"Cannot read Int32 from {cnt} bytes");

            if (cnt == 0)
                return 0;

            int res = bytes[cnt - 1];
            for (var i = cnt - 2; i >= 0; i--)
                res = (res << 8) + bytes[i];

            return res;
        }

        public static long GetInt64Reverse(byte[] bytes)
        {
            var cnt = bytes.Length;
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                if (bytes[i] == 0) cnt--;
                else break;
            }

            if (cnt > 8)
                throw new Exception($"Cannot read Int64 from {cnt} bytes");
            
            if (cnt == 0)
                return 0;

            int res = bytes[cnt - 1];
            for (var i = cnt - 2; i >= 0; i--)
                res = (res << 8) + bytes[i];

            return res;
        }

        public static byte[] GetBytes(int value)
        {
            var bytes = new List<byte>(4);
            do
            {
                bytes.Add((byte)(value & 0xff));
                value >>= 8;
            }
            while (value != 0);


            var res = new byte[bytes.Count];
            for (var i = 0; i < bytes.Count; i++)
                res[res.Length - 1 - i] = bytes[i];

            return res;
        }

        public static int GetInt32(string hex)
        {
            if (hex.Length < 3 || hex[1] != 'x' || hex.Length > 10)
                throw new Exception("Invalid int32 hex");

            int res = HexAscii[hex[2]];
            for (var i = 3; i < hex.Length; i++)
                res = (res << 4) + HexAscii[hex[i]];

            return res;
        }

        public static long GetInt64(string hex)
        {
            if (hex.Length < 3 || hex[1] != 'x' || hex.Length > 18)
                throw new Exception("Invalid int64 hex");

            long res = HexAscii[hex[2]];
            for (var i = 3; i < hex.Length; i++)
                res = (res << 4) + HexAscii[hex[i]];

            return res;
        }

        public static ulong GetUInt64(string hex)
        {
            if (hex.Length < 3 || hex[1] != 'x' || hex.Length > 18)
                throw new Exception("Invalid uint64 hex");

            ulong res = (uint)HexAscii[hex[2]];
            for (var i = 3; i < hex.Length; i++)
                res = (res << 4) + (uint)HexAscii[hex[i]];

            return res;
        }

        public static BigInteger GetBigInteger(string hex)
        {
            var pos = hex.Length >= 2 && hex[1] == 'x' ? 2 : 0;
            var digits = hex.Length - pos;

            if (digits == 0)
                return BigInteger.Zero;

            var size = (digits + 1) >> 1;
            Span<byte> bytes = size <= 32 ? stackalloc byte[32] : new byte[size];
            bytes = bytes[..size];

            var i = 0;
            if ((digits & 1) != 0)
            {
                if (hex[pos] > 102) throw new FormatException("Invalid bigint hex");
                int p = HexAscii[hex[pos++]];

                if (p == 255) throw new FormatException("Invalid bigint hex");
                bytes[i++] = (byte)p;
            }

            for (; i < size; i++)
            {
                if (hex[pos] > 102) throw new FormatException("Invalid bigint hex");
                int h = HexAscii[hex[pos++]];

                if (hex[pos] > 102) throw new FormatException("Invalid bigint hex");
                int l = HexAscii[hex[pos++]];

                if ((h | l) == 255) throw new FormatException("Invalid bigint hex");
                bytes[i] = (byte)((h << 4) + l);
            }

            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }

        public static DateTime GetTimestamp(string hex)
        {
            return DateTime.UnixEpoch.AddSeconds(GetInt32(hex));
        }

        public static DateTime GetTimestampMs(string hex)
        {
            return DateTime.UnixEpoch.AddMilliseconds(GetInt64(hex));
        }
    }
}
