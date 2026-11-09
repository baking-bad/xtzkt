using Blake2Fast;
using Netezos;
using Netezos.Encoding;
using Xtzkt.Utils.Crypto;

namespace Xtzkt.Utils;

public static class Runtimes
{
    /// <summary>
    /// Returns legacy EVM alias (`0x...`) of the specified address.
    /// </summary>
    public static string GetLegacyEvmAlias(string address)
    {
        return Keccak256.GetHash(Encoding.Utf8.GetBytes(address), 20);
    }

    /// <summary>
    /// Returns recoverable EVM alias (`0x...`, RFC-26051) of the specified `tz1`/`tz2`/`tz3` address.
    /// </summary>
    public static string? GetRecoverableEvmAlias(string address)
    {
        byte[] prefix, impl;
        if (address.StartsWith("tz1", StringComparison.Ordinal))
            (prefix, impl) = (Prefixes.tz1, RescueImplTz1);
        else if (address.StartsWith("tz2", StringComparison.Ordinal))
            (prefix, impl) = (Prefixes.tz2, RescueImplTz2);
        else if (address.StartsWith("tz3", StringComparison.Ordinal))
            (prefix, impl) = (Prefixes.tz3, RescueImplTz3);
        else
            return null;

        if (!Encoding.Base58.TryDecode(address, prefix, out var pkh))
            throw new Exception($"Failed to decode address {address}");

        byte[] initcode = [0x73, .. RescueGuard, .. RescueGuardTail, .. RescueProxyHead, .. impl, .. RescueProxyTail, .. pkh];
        byte[] create2 = [0xff, .. RescueSafeFactory, .. RescueSalt, .. Keccak256.GetHashBytes(initcode)];

        return Encoding.Hex.GetString(Keccak256.GetHashBytes(create2).AsSpan(12));
    }

    /// <summary>
    /// Returns Michelson alias (`KT1...`) of the specified address.
    /// </summary>
    public static string GetMichelsonAlias(string address)
    {
        return Base58.Convert(Blake2b.ComputeHash(20, Encoding.Utf8.GetBytes(address)), Prefixes.KT1);
    }

    #region RFC-26051, recoverable_alias.rs
    static readonly byte[] RescueSafeFactory = Encoding.Hex.GetBytes("914d7fec6aac8cd542e72bca78b30650d45643d7");
    static readonly byte[] RescueSalt = Encoding.Hex.GetBytes("9b8bb449a3d23b110d8c46424ba9b40748ac28bc7b99b7422a272950e17bb1d8");
    static readonly byte[] RescueGuard = Encoding.Hex.GetBytes("3822d2fe3dce6ce4f4c7f274e2415fa3fae66c3e");
    static readonly byte[] RescueGuardTail = Encoding.Hex.GetBytes("3b15601e57600080fd5b604180602a6000396000f3");
    static readonly byte[] RescueProxyHead = Encoding.Hex.GetBytes("363d3d373d3d3d363d73");
    static readonly byte[] RescueProxyTail = Encoding.Hex.GetBytes("5af43d82803e903d91602b57fd5bf3");
    static readonly byte[] RescueImplTz1 = Encoding.Hex.GetBytes("fee60e905d91905df9cd6bf8153976e9d77d5051");
    static readonly byte[] RescueImplTz2 = Encoding.Hex.GetBytes("47c81e55f51344d80a8e85c2add86257b51ea3d2");
    static readonly byte[] RescueImplTz3 = Encoding.Hex.GetBytes("894b62c61b208184b80fd72803811ba35d818112");
    #endregion
}
