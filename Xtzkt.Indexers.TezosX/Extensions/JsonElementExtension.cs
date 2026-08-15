using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Extensions;

public static class JsonElementExtension
{
    public static EvmOpType RequiredEvmOpType(this JsonElement el, string name)
    {
        var value = el.RequiredHexInt32(name);
        return value switch
        {
            0 => EvmOpType.Legacy,
            1 => EvmOpType.AccessList,
            2 => EvmOpType.DynamicFee,
            //3 => EvmOpType.Blob,
            4 => EvmOpType.SetCode,
            _ => throw new NotSupportedException($"Evm op type {value} is not supported"),
        };
    }

    public static EvmOpCode RequiredEvmOpCode(this JsonElement el, string name)
    {
        var value = el.RequiredString(name);
        return value switch
        {
            "CREATE" => EvmOpCode.Create,
            "CREATE2" => EvmOpCode.Create2,
            "CALL" => EvmOpCode.Call,
            "CALLCODE" => EvmOpCode.CallCode,
            "DELEGATECALL" => EvmOpCode.DelegateCall,
            "STATICCALL" => EvmOpCode.StaticCall,
            "SELFDESTRUCT" => EvmOpCode.SelfDestruct,
            "SUICIDE" => EvmOpCode.Suicide,
            _ => throw new NotSupportedException($"Evm op code {value} is not supported"),
        };
    }

    public static OperationStatus RequiredEvmOpStatus(this JsonElement el, string name)
    {
        var value = el.RequiredHexInt32(name);
        return value switch
        {
            0 => OperationStatus.Failed,
            1 => OperationStatus.Applied,
            _ => throw new NotSupportedException($"Evm op status {value} is not supported"),
        };
    }

    public static OperationStatus RequiredOpStatus(this JsonElement el, string name)
    {
        return el.RequiredString(name) switch
        {
            "applied" => OperationStatus.Applied,
            "backtracked" => OperationStatus.Backtracked,
            "failed" => OperationStatus.Failed,
            "skipped" => OperationStatus.Skipped,
            _ => throw new NotImplementedException()
        };
    }
}
