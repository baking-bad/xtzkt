using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class Directions
{
    public const string L1 = "l1";
    public const string XEvm = "x_evm";
    public const string XMichelson = "x_michelson";
    public const string XEvmMichelson = "x_evm_michelson";
    public const string XMichelsonEvm = "x_michelson_evm";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { L1, (int)Direction.L1 },
        { XEvm, (int)Direction.XEvm },
        { XMichelson, (int)Direction.XMichelson },
        { XEvmMichelson, (int)Direction.XEvmMichelson },
        { XMichelsonEvm, (int)Direction.XMichelsonEvm },
    };

    public static string ToString(int value) => value switch
    {
        (int)Direction.L1 => L1,
        (int)Direction.XEvm => XEvm,
        (int)Direction.XMichelson => XMichelson,
        (int)Direction.XEvmMichelson => XEvmMichelson,
        (int)Direction.XMichelsonEvm => XMichelsonEvm,
        _ => throw new Exception("invalid value")
    };
}
