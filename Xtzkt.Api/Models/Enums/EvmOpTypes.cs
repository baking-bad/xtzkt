using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class EvmOpTypes
{
    public const string Legacy     = "legacy";
    public const string AccessList = "access_list";
    public const string DynamicFee = "dynamic_fee";
    public const string Blob       = "blob";
    public const string SetCode    = "set_code";
    public const string Trace      = "trace";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Legacy,     (int)EvmOpType.Legacy },
        { AccessList, (int)EvmOpType.AccessList },
        { DynamicFee, (int)EvmOpType.DynamicFee },
        { Blob,       (int)EvmOpType.Blob },
        { SetCode,    (int)EvmOpType.SetCode },
        { Trace,      (int)EvmOpType.Trace },
    };

    public static string ToString(int value) => value switch
    {
        (int)EvmOpType.Legacy     => Legacy,
        (int)EvmOpType.AccessList => AccessList,
        (int)EvmOpType.DynamicFee => DynamicFee,
        (int)EvmOpType.Blob       => Blob,
        (int)EvmOpType.SetCode    => SetCode,
        (int)EvmOpType.Trace      => Trace,
        _ => throw new Exception("invalid value")
    };
}
