using System.Numerics;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Utils.Abi;

public static class EvmValueDecoder
{
    const int WordSize = 32;

    public static bool TryDecodeString(ReadOnlySpan<byte> data, out string value)
    {
        value = string.Empty;
        if (data.Length == 0)
            return false;

        // legacy bytes32
        if (data.Length == WordSize)
        {
            var end = data.Length - 1;
            while (data[end] == 0 && --end >= 0) ;
            value = Utf8.GetString(data[..(end + 1)]);
            return true;
        }

        if (!TryReadLength(data, out var offset) || offset > data.Length)
            return false;

        if (!TryReadLength(data[offset..], out var length))
            return false;

        offset += WordSize;
        if (length > data.Length - offset)
            return false;

        value = Utf8.GetString(data.Slice(offset, length));
        return true;
    }

    public static bool TryDecodeUInt256(ReadOnlySpan<byte> data, out BigInteger value)
    {
        value = default;

        if (data.Length < WordSize)
            return false;

        value = new BigInteger(data[..WordSize], true, true);
        return true;
    }

    public static bool TryDecodeByte(ReadOnlySpan<byte> data, out byte value)
    {
        value = default;
        
        if (data.Length < WordSize)
            return false;
        
        for (var i = 0; i < 31; i++)
            if (data[i] != 0)
                return false;
        
        value = data[31];
        return true;
    }

    static bool TryReadLength(ReadOnlySpan<byte> data, out int value)
    {
        value = default;

        if (data.Length < WordSize)
            return false;

        var pos = 0;
        while (data[pos] == 0 && pos < 31)
            pos++;

        if (pos < 28 || pos == 28 && (data[28] & 0x80) != 0)
            return false;

        value = data[pos];
        for (var i = pos + 1; i < WordSize; i++)
            value = (value << 8) | data[i];

        return true;
    }
}
