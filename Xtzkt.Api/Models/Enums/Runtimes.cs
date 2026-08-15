using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class Runtimes
{
    public const string Evm = "evm";
    public const string Michelson = "michelson";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Evm, (int)Runtime.Evm },
        { Michelson, (int)Runtime.Michelson },
    };

    public static string ToString(int value) => value switch
    {
        (int)Runtime.Evm => Evm,
        (int)Runtime.Michelson => Michelson,
        _ => throw new Exception("invalid value")
    };
}
