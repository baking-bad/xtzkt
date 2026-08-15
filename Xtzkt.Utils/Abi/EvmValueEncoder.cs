using System.Numerics;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Utils.Abi;

public static class EvmValueEncoder
{
    const int WordSize = 32;

    public static byte[] EncodeUInt256(BigInteger value)
    {
        if (value.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "uint256 must be positive");

        var bytes = value.ToByteArray(true, true);
        if (bytes.Length > WordSize)
            throw new ArgumentOutOfRangeException(nameof(value), "value exceeds uint256");

        var word = new byte[WordSize];
        new ReadOnlySpan<byte>(bytes).CopyTo(word.AsSpan(word.Length - bytes.Length));
        return word;
    }

    public static string EncodeCallData(string selector, BigInteger arg)
    {
        return selector + Hex.GetStringRaw(EncodeUInt256(arg));
    }
}
