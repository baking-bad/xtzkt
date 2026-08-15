using Xtzkt.Data.Models;

namespace Xtzkt.Api.Models.Enums;

internal static class ContractKinds
{
    public const string DelegatorContract = "delegator_contract";
    public const string SmartContract     = "smart_contract";
    public const string Asset             = "asset";

    public static readonly Dictionary<string, int> Mapping = new()
    {
        { DelegatorContract, (int)AllContractKind.DelegatorContract },
        { SmartContract,     (int)AllContractKind.SmartContract },
        { Asset,             (int)AllContractKind.Asset },
    };

    public static string ToString(int value) => value switch
    {
        (int)AllContractKind.DelegatorContract => DelegatorContract,
        (int)AllContractKind.SmartContract     => SmartContract,
        (int)AllContractKind.Asset             => Asset,
        _ => throw new Exception("invalid value")
    };
}
