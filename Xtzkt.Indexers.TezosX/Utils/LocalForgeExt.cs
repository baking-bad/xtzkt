using System.Text.Json;
using Netezos.Forging;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Utils;

public static class LocalForgeExt
{
    public static int SafeMichelineSize(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object && el.OptionalHexBytes("unparsed-binary") is byte[] bin)
            return bin.Length;

        return LocalForge.ForgeArray(LocalForge.ForgeMicheline(Netezos.Encoding.Micheline.FromJson(el)!)).Length;
    }

    // TODO: re-check tz4 and tz5 sizes once the kernel supports them
    public static int SignatureSize(string signer) =>
        signer.StartsWith("tz5") ? 2420 + 2 : // ML-DSA-44 + ff 04
        signer.StartsWith("tz4") ? 96 + 2 :   // BLS + ff 03
        64;                                   // Ed25519, Secp256k1, P256
}
