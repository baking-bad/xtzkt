using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class EvmRuntime : Proto01.EvmRuntime
{
    #region precompiles with known ABI
    public override string MichelsonGateway => "0xff00000000000000000000000000000000000007";

    public override string AliasForwarder => "0xff00000000000000000000000000000000ffff08";

    public override string VerifyTezosSignature => "0xff0000000000000000000000000000000000000a";
    #endregion

    #region special addresses
    public override string TezosXCaller => "0x7e20580000000000000000000000000000000001";
    #endregion

    #region known topics
    /*
    event CrossRuntimeCallReceived(
        string crossRuntimeCallId,
        string sourceRuntime,
        string senderAddress,
        string sourceAddress,
        string targetAddress,
        uint256 amount
    );
    */
    public override string CracReceivedTopic => "0xfed9a84c1a0b3f03a08e089ccd124decdf96e24756f1963dbcef903c803d5b09";

    /*
    event CrossRuntimeCallSent(
        string crossRuntimeCallId,
        string targetRuntime,
        string targetAddress,
        uint256 amount
    );
    */
    public override string CracSentTopic => "0x63d7b3745d574412126b88bc586adea349b617cd75e87d47aff3dbc765bd834b";

    // Initialized(string,bytes,uint256)
    public override string AliasInitializedTopic => "0x60a9f8ac7be7e117b08e5ff52239667fcf051d55e03ead4bfa34c73ff86642e0";

    // Forwarded(string,uint256)
    public override string AliasForwardedTopic => "0x6056e4b67a875e88fea78aa81a94c37d06242e7b7d71d02fb18f8e1b9c0ac97c";
    #endregion

    #region known selectors
    // callMichelson(string,string,bytes)
    public override string CallMichelsonSelector => "0xa1544fc3";

    // call(string,(string,string)[],bytes,uint8)
    public override string CallSelector => "0xfa591a56";
    #endregion

    #region helpers
    public override string GetAlias(string address)
    {
        return Runtimes.GetEvmAlias(address);
    }

    public override int ConvertGas(long michelsonMilligas)
    {
        // etherlink/kernel_latest/tezosx-constants/src/lib.rs: EVM_GAS_TO_MILLIGAS
        const int evmGasToMilligas = 22;
        return (int)((michelsonMilligas + evmGasToMilligas - 1) / evmGasToMilligas);
    }

    public override bool IsCracCall(string? to, JsonElement trace)
    {
        // TODO: figure out how to exclude crac calls that failed before reaching the other side
        // to not consume others' crac calls

        if (to != MichelsonGateway || trace.OptionalString("input") is not string input)
            return false;

        // a stateful call is restricted under these, so such an attempt reverts without reaching
        // the other runtime, unlike a normally reverted call, which leaves a backtracked operation
        if (trace.RequiredString("type") is "STATICCALL" or "DELEGATECALL" or "CALLCODE")
            return false;

        if (input.StartsWith(CallMichelsonSelector, StringComparison.OrdinalIgnoreCase))
            return true;

        if (input.StartsWith(CallSelector, StringComparison.OrdinalIgnoreCase))
        {
            const int methodOffset = 2 + 8 + 3 * 64; // 0x + selector + 3 first args pointers
            return input.Length >= methodOffset + 64
                && input.AsSpan(methodOffset, 64).TrimStart('0') is ['1']; // 0 = GET (read-only), 1 = POST (stateful)
        }

        return false;
    }
    #endregion
}
