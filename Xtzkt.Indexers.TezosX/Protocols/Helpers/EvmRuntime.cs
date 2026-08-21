using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols;

public static class EvmRuntime
{
    public const string RuntimeId = "1";

    #region precompiles with known ABI
    public const string NullAddress = "0x0000000000000000000000000000000000000000";
    public const string XtzBridge = "0xff00000000000000000000000000000000000001";
    public const string FaBridge = "0xff00000000000000000000000000000000000002";
    public const string Outbox = "0xff00000000000000000000000000000000000003";
    public const string TicketTable = "0xff00000000000000000000000000000000000004";
    public const string GlobalCounter = "0xff00000000000000000000000000000000000005";
    public const string SequencerUpdater = "0xff00000000000000000000000000000000000006";
    public const string MichelsonGateway = "0xff00000000000000000000000000000000000007";
    public const string AliasForwarder = "0xff00000000000000000000000000000000ffff08";
    public const string VerifyTezosSignature = "0xff0000000000000000000000000000000000000a";
    #endregion

    #region special addresses
    public const string TezosXCaller = "0x7e20580000000000000000000000000000000001";
    public const string DepositOrigin = "0x000000000000000000000000000000000000feed";
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
    public const string CracReceivedTopic = "0xfed9a84c1a0b3f03a08e089ccd124decdf96e24756f1963dbcef903c803d5b09";

    /*
    event CrossRuntimeCallSent(
        string crossRuntimeCallId,
        string targetRuntime,
        string targetAddress,
        uint256 amount
    );
    */
    public const string CracSentTopic = "0x63d7b3745d574412126b88bc586adea349b617cd75e87d47aff3dbc765bd834b";

    // Initialized(string,bytes,uint256)
    public const string AliasInitializedTopic = "0x60a9f8ac7be7e117b08e5ff52239667fcf051d55e03ead4bfa34c73ff86642e0";

    // Forwarded(string,uint256)
    public const string AliasForwardedTopic = "0x6056e4b67a875e88fea78aa81a94c37d06242e7b7d71d02fb18f8e1b9c0ac97c";
    #endregion

    #region known selectors
    // callMichelson(string,string,bytes)
    public const string CallMichelsonSelector = "0xa1544fc3";

    // call(string,(string,string)[],bytes,uint8)
    public const string CallSelector = "0xfa591a56";

    // drains a queued FA deposit; unchanged since the queue appeared in the kernel
    public static readonly byte[] FaClaimSelector = Selector("claim(uint256)");

    // drains a queued XTZ deposit; exists since the XTZ bridge got a queue of its own
    public static readonly byte[] XtzClaimSelector = Selector("claim_xtz(uint256)");
    #endregion

    static byte[] Selector(string signature) =>
        Keccak256.GetHashBytes(Utf8.GetBytes(signature))[..4];

    public static string GetAlias(string address)
    {
        return Runtimes.GetEvmAlias(address);
    }

    public static int ConvertGas(long michelsonMilligas)
    {
        // etherlink/kernel_latest/tezosx-constants/src/lib.rs: EVM_GAS_TO_MILLIGAS
        const int evmGasToMilligas = 22;
        return (int)((michelsonMilligas + evmGasToMilligas - 1) / evmGasToMilligas);
    }

    public static bool IsCracCall(string? to, JsonElement trace)
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
}
