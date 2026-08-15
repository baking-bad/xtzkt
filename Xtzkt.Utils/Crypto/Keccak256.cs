using Org.BouncyCastle.Crypto.Digests;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Utils.Crypto;

public static class Keccak256
{
    public static byte[] GetHashBytes(byte[] data)
    {
        var res = new byte[32];

        var keccak = new KeccakDigest(256);
        keccak.BlockUpdate(data, 0, data.Length);
        keccak.DoFinal(res, 0);

        return res;
    }

    public static string GetHash(byte[] data)
    {
        var res = new byte[32];

        var keccak = new KeccakDigest(256);
        keccak.BlockUpdate(data, 0, data.Length);
        keccak.DoFinal(res, 0);

        return Hex.GetString(res);
    }

    public static string GetHash(byte[] data, int size)
    {
        var res = new byte[32];

        var keccak = new KeccakDigest(256);
        keccak.BlockUpdate(data, 0, data.Length);
        keccak.DoFinal(res, 0);

        return Hex.GetString(res.AsSpan()[0..size]);
    }

    public static string GetEvmAddress(byte[] pubkey)
    {
        return Hex.GetString(GetHashBytes(pubkey).AsSpan()[12..]);
    }
}
