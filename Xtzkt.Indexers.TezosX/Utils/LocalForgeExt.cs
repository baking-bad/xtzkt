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
}
