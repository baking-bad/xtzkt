using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class PvmKinds
{
    public const string Arith = "arith";
    public const string Wasm  = "wasm";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Arith, (int)PvmKind.Arith },
        { Wasm,  (int)PvmKind.Wasm },
    };

    public static string ToString(int value) => value switch
    {
        (int)PvmKind.Arith => Arith,
        (int)PvmKind.Wasm  => Wasm,
        _ => throw new Exception("invalid value")
    };
}
