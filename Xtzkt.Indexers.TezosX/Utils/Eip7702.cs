using System.Numerics;
using System.Text.Json;
using Secp256k1Net;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Utils;

public static class Eip7702
{
    const byte Magic = 0x05;
    static readonly BigInteger LowS = new(Hex.GetBytes("0x7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF5D576E7357A4501DDFE92F46681B20A0"), true, true);

    public static (BigInteger chainId, string contract, ulong nonce, string authority) ParseAuthorization(JsonElement authorization)
    {
        var chainIdStr = authorization.RequiredString("chainId");
        var chainIdBytes = TrimZeros(Hex.GetBytes(NormalizeHex(chainIdStr)));
        if (chainIdBytes.Length > 32)
            throw new Exception("Invalid 'chainId'");

        var addressStr = authorization.RequiredString("address");
        var address = Hex.GetBytes(addressStr);
        if (address.Length != 20)
            throw new Exception("Invalid 'address'");

        var nonceStr = authorization.RequiredString("nonce");
        var nonceBytes = TrimZeros(Hex.GetBytes(NormalizeHex(nonceStr)));
        if (nonceBytes.Length > 8)
            throw new Exception("Invalid 'nonce'");

        var nonce = HexNumber.GetUInt64(nonceBytes);
        if (nonce == ulong.MaxValue)
            throw new Exception("Invalid 'nonce'");

        var yParityStr = authorization.RequiredString("yParity");
        var yParity = (byte)(yParityStr == "0x0" ? 0 : yParityStr == "0x1" ? 1 : 2);
        if (yParity == 2)
            throw new Exception("Invalid 'yParity'");

        var rStr = authorization.RequiredString("r");
        var r = TrimZeros(Hex.GetBytes(NormalizeHex(rStr)));
        if (r.Length > 32)
            throw new Exception("Invalid 'r'");

        var sStr = authorization.RequiredString("s");
        var s = TrimZeros(Hex.GetBytes(NormalizeHex(sStr)));
        if (s.Length > 32 || new BigInteger(s, true, true) > LowS)
            throw new Exception("Invalid 's'");

        var message = RlpEncodeList([
            .. RlpEncodeBytes(chainIdBytes),
            .. RlpEncodeBytes(address),
            .. RlpEncodeBytes(nonceBytes),
        ]);
        var messageHash = Keccak256.GetHashBytes([Magic, .. message]);

        var pubKey = Secp256k1.RecoverPublicKey(ToSignature(r, s), yParity, messageHash, false);
        var authority = Keccak256.GetEvmAddress(pubKey[1..]);

        return (new BigInteger(chainIdBytes, true, true), addressStr, nonce, authority);
    }

    static string NormalizeHex(string hex) => (hex.Length & 0x1) != 0 ? "0x0" + hex[2..] : hex;

    static byte[] TrimZeros(byte[] bytes)
    {
        var pos = 0;
        while(pos < bytes.Length && bytes[pos] == 0)
            pos++;

        return pos == 0 ? bytes : pos == bytes.Length ? [] : bytes[pos..];
    }

    static byte[] ToSignature(byte[] r, byte[] s)
    {
        var res = new byte[64];
        Buffer.BlockCopy(r, 0, res, 32 - r.Length, r.Length);
        Buffer.BlockCopy(s, 0, res, 64 - s.Length, s.Length);
        return res;
    }

    static byte[] RlpEncodeBytes(byte[] data)
    {
        if (data.Length == 1 && data[0] < 0x80) return data;
        return [.. RlpEncodeLength(data.Length, 0x80), .. data];
    }

    static byte[] RlpEncodeList(byte[] items) =>
        [.. RlpEncodeLength(items.Length, 0xc0), .. items];

    static byte[] RlpEncodeLength(int length, byte @base)
    {
        if (length <= 55)
            return [(byte)(@base + length)];

        Span<byte> buf = stackalloc byte[4];
        var i = buf.Length;
        while (length != 0)
        {
            buf[--i] = (byte)(length & 0xff);
            length >>= 8;
        }

        return [(byte)(@base + 55 + (buf.Length - i)), .. buf[i..]];
    }
}
