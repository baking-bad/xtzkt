using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class AddressTypes
{
    public const string L1User         = "l1_user";
    public const string L1Baker        = "l1_baker";
    public const string L1Contract     = "l1_contract";
    public const string L1SmartRollup  = "l1_smart_rollup";
    public const string L1Ghost        = "l1_ghost";
    public const string XEvmUser       = "x_evm_user";
    public const string XEvmAlias      = "x_evm_alias";
    public const string XEvmContract   = "x_evm_contract";
    public const string XMichelsonUser     = "x_michelson_user";
    public const string XMichelsonAlias    = "x_michelson_alias";
    public const string XMichelsonContract = "x_michelson_contract";
    public const string XMichelsonGhost    = "x_michelson_ghost";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { L1User,             (int)AddressType.L1User },
        { L1Baker,            (int)AddressType.L1Baker },
        { L1Contract,         (int)AddressType.L1Contract },
        { L1SmartRollup,      (int)AddressType.L1SmartRollup },
        { L1Ghost,            (int)AddressType.L1Ghost },
        { XEvmUser,           (int)AddressType.XEvmUser },
        { XEvmAlias,          (int)AddressType.XEvmAlias },
        { XEvmContract,       (int)AddressType.XEvmContract },
        { XMichelsonUser,     (int)AddressType.XMichelsonUser },
        { XMichelsonAlias,    (int)AddressType.XMichelsonAlias },
        { XMichelsonContract, (int)AddressType.XMichelsonContract },
        { XMichelsonGhost,    (int)AddressType.XMichelsonGhost },
    };

    public static string ToString(int value) => value switch
    {
        (int)AddressType.L1User             => L1User,
        (int)AddressType.L1Baker            => L1Baker,
        (int)AddressType.L1Contract         => L1Contract,
        (int)AddressType.L1SmartRollup      => L1SmartRollup,
        (int)AddressType.L1Ghost            => L1Ghost,
        (int)AddressType.XEvmUser           => XEvmUser,
        (int)AddressType.XEvmAlias          => XEvmAlias,
        (int)AddressType.XEvmContract       => XEvmContract,
        (int)AddressType.XMichelsonUser     => XMichelsonUser,
        (int)AddressType.XMichelsonAlias    => XMichelsonAlias,
        (int)AddressType.XMichelsonContract => XMichelsonContract,
        (int)AddressType.XMichelsonGhost    => XMichelsonGhost,
        _ => throw new Exception("invalid value")
    };
}
