using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class DepositTypes
{
    public const string Xtz = "xtz";
    public const string Fa = "fa";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Xtz, (int)DepositType.Xtz },
        { Fa, (int)DepositType.Fa },
    };

    public static string ToString(int value) => value switch
    {
        (int)DepositType.Xtz => Xtz,
        (int)DepositType.Fa => Fa,
        _ => throw new Exception("invalid value")
    };
}
