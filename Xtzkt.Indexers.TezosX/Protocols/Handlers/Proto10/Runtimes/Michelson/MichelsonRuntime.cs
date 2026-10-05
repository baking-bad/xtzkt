using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

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
            if (parameters.TryGetProperty("value", out var value) && Micheline.GetCombInt(value, 3) is string method)
                return method != "0";
        }

        return false;
    }
    #endregion
}
