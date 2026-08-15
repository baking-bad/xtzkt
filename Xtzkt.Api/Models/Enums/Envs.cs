using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class Envs
{
    public const string L1 = "l1";
    public const string XEvm = "x_evm";
    public const string XMichelson = "x_michelson";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { L1, (int)Env.L1 },
        { XEvm, (int)Env.XEvm },
        { XMichelson, (int)Env.XMichelson },
    };

    public static string ToString(int value) => value switch
    {
        (int)Env.L1 => L1,
        (int)Env.XEvm => XEvm,
        (int)Env.XMichelson => XMichelson,
        _ => throw new Exception("invalid value")
    };
}
