namespace Xtzkt.Indexers.TezosX.Utils
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

        public static ulong GetUInt64(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > 8)
                throw new Exception("Invalid UInt64 bytes");

            if (bytes.Length == 0)
                return 0;

            ulong res = bytes[0];
            for (var i = 1; i < bytes.Length; i++)
                res = (res << 8) | bytes[i];

            return res;
        }

        public static int GetInt32(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > 4)
                throw new Exception("Invalid Int32 bytes");

            if (bytes.Length == 0)
                return 0;

            int res = bytes[0];
            for (var i = 1; i < bytes.Length; i++)
                res = (res << 8) | bytes[i];

            return res;
        }

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

            long res = bytes[cnt - 1];
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
                throw new Exception("Invalid hex number");

            hex = hex[2..];

            int res = HexAscii[hex[0]];
            for (var i = 1; i < hex.Length; i++)
                res = (res << 4) + HexAscii[hex[i]];

            return res;
        }

        public static long GetInt64(string hex)
        {
            if (hex.Length < 2 || hex[1] != 'x' || hex.Length > 18)
                throw new Exception("Invalid hex string");

            hex = hex[2..];

            long res = HexAscii[hex[0]];
            for (var i = 1; i < hex.Length; i++)
                res = (res << 4) + HexAscii[hex[i]];

            return res;
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
