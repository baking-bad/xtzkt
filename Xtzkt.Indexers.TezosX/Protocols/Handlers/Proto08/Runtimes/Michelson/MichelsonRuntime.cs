using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

public class MichelsonRuntime : IMichelsonRuntime
{
    public string RuntimeId => "0";

    #region special addresses
    public string NullAddress => "tz1Ke2h7sDdakHJQh8WX4Z372du1KChsksyU";

    public string EvmGateway => "KT18oDJJKXMKhfE1bSuAPGp92pYcwVDiqsPw";

    public string CracOrigin => "tz1Ke2h7sDdakHJQh8WX4Z372du1KChsksyU";

    public string DepositOrigin => "tz1Ke2h7sDdakHJQh8WX4Z372du1KChsksyU";
    #endregion

    #region helpers
    public string GetAlias(string address)
    {
        return Runtimes.GetMichelsonAlias(address);
    }

    public int ConvertGas(int evmGas)
    {
        // etherlink/kernel_latest/tezosx-constants/src/lib.rs: EVM_GAS_TO_MILLIGAS
        const int evmGasToMilligas = 22;
        return (evmGas * evmGasToMilligas + 999) / 1000;
    }

    public bool IsCracCall(string? to, JsonElement content)
    {
        // TODO: figure out how to exclude crac calls that failed before reaching the other side
        // to not consume others' crac calls

        if (to != EvmGateway || content.Optional("parameters") is not JsonElement parameters)
            return false;

        var ep = parameters.RequiredString("entrypoint");
        if (ep == "call_evm")
            return true;

        if (ep == "call")
        {
            // %call parameter is a right comb - (url, headers, body, method, callback).
            // unlike the evm side, which reverts on an unknown method, this one falls back to POST,
            // this is why we compare to "0" instead of "1"
            if (parameters.TryGetProperty("value", out var value) && GetInt(GetCombElement(value, 3)) is string method)
                return method != "0";
        }

        return false;
    }
    #endregion

    #region utils
    static JsonElement? GetCombElement(JsonElement value, int index)
    {
        while (index >= 0)
        {
            if (GetCombArgs(value) is not JsonElement args)
                return index == 0 ? value : null;

            var count = args.GetArrayLength();
            if (count < 2)
                return null;

            if (index < count - 1)
                return args[index];

            index -= count - 1;
            value = args[count - 1];
        }
        return null;
    }

    static JsonElement? GetCombArgs(JsonElement value)
    {
        // a comb can be serialized as a sequence, as a nested prim or as a flattened prim
        if (value.ValueKind == JsonValueKind.Array)
            return value;

        if (value.ValueKind != JsonValueKind.Object)
            return null;

        if (!value.TryGetProperty("prim", out var prim) ||
            prim.ValueKind != JsonValueKind.String ||
            !prim.ValueEquals("Pair"))
            return null;

        return value.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array
            ? args
            : null;
    }

    static string? GetInt(JsonElement? value)
    {
        return value?.ValueKind == JsonValueKind.Object &&
            value.Value.TryGetProperty("int", out var res) &&
            res.ValueKind == JsonValueKind.String
                ? res.GetString()
                : null;
    }
    #endregion
}
