using Blake2Fast;
using Netezos;
using Netezos.Encoding;
using Xtzkt.Utils.Crypto;

namespace Xtzkt.Utils;

public static class Runtimes
{
    /// <summary>
    /// Returns EVM alias (`0x...`) of the specified address.
    /// </summary>
    public static string GetEvmAlias(string address)
    {
        return Keccak256.GetHash(Encoding.Utf8.GetBytes(address), 20);
    }

    /// <summary>
    /// Returns Michelson alias (`KT1...`) of the specified address.
    /// </summary>
    public static string GetMichelsonAlias(string address)
    {
        return Base58.Convert(Blake2b.ComputeHash(20, Encoding.Utf8.GetBytes(address)), Prefixes.KT1);
    }
}
