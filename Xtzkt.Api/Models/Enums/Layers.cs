using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class Layers
{
    public const string L1 = "l1";
    public const string TezosX = "x";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { L1, (int)Layer.L1 },
        { TezosX, (int)Layer.TezosX },
    };

    public static string ToString(int value) => value switch
    {
        (int)Layer.L1 => L1,
        (int)Layer.TezosX => TezosX,
        _ => throw new Exception("invalid value")
    };
}
