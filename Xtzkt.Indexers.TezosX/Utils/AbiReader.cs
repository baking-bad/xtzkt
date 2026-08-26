using System.Numerics;
using System.Text;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Utils
{
    sealed class AbiReader(byte[] data)
    {
        readonly byte[] _data = data;

        public BigInteger ReadUInt256(int slot)
        {
            var pos = slot * 32;
            if (pos + 32 > _data.Length)
                throw new FormatException("Out of bounds");

            return new BigInteger(_data.AsSpan(pos, 32), true, true);
        }

        public string ReadString(int slot)
        {
            var offset = ReadInt32BE(slot * 32);
            var length = ReadInt32BE(offset);
            offset += 32;

            if (offset + length > _data.Length)
                throw new FormatException("Out of bounds");

            return Encoding.UTF8.GetString(_data.AsSpan(offset, length));
        }

        int ReadInt32BE(int pos)
        {
            if (pos + 32 > _data.Length)
                throw new FormatException("Out of bounds");

            var offset = 0;
            while (_data[pos + offset] == 0 && ++offset < 32) ;

            if (offset < 28)
                throw new FormatException("Int32 out of bounds");

            int res = 0;
            for (var i = offset; i < 32; i++)
                res = (res << 8) | _data[pos + i];
            
            return res;
        }

        public static string ReadString(byte[] data, int offset)
        {
            return Encoding.UTF8.GetString(data.AsSpan(offset));
        }

        public static byte ReadUint8(byte[] data, int offset)
        {
            return data.AsSpan(offset)[0];
        }

        public static string ReadAddress(byte[] data, int offset)
        {
            return Hex.GetString(data.AsSpan(offset, 32));
        }

        public static BigInteger ReadUint256(byte[] data, int offset)
        {
            return new BigInteger(data.AsSpan(offset, 32), true, true);
        }
    }
}
