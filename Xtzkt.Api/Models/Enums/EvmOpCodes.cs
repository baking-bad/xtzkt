using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class EvmOpCodes
{
    public const string Create       = "create";
    public const string Create2      = "create2";
    public const string Call         = "call";
    public const string CallCode     = "call_code";
    public const string DelegateCall = "delegate_call";
    public const string StaticCall   = "static_call";
    public const string SelfDestruct = "self_destruct";
    public const string Suicide      = "suicide";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { Create,       (int)EvmOpCode.Create },
        { Create2,      (int)EvmOpCode.Create2 },
        { Call,         (int)EvmOpCode.Call },
        { CallCode,     (int)EvmOpCode.CallCode },
        { DelegateCall, (int)EvmOpCode.DelegateCall },
        { StaticCall,   (int)EvmOpCode.StaticCall },
        { SelfDestruct, (int)EvmOpCode.SelfDestruct },
        { Suicide,      (int)EvmOpCode.Suicide },
    };

    public static string ToString(int value) => value switch
    {
        (int)EvmOpCode.Create       => Create,
        (int)EvmOpCode.Create2      => Create2,
        (int)EvmOpCode.Call         => Call,
        (int)EvmOpCode.CallCode     => CallCode,
        (int)EvmOpCode.DelegateCall => DelegateCall,
        (int)EvmOpCode.StaticCall   => StaticCall,
        (int)EvmOpCode.SelfDestruct => SelfDestruct,
        (int)EvmOpCode.Suicide      => Suicide,
        _ => throw new Exception("invalid value")
    };
}
