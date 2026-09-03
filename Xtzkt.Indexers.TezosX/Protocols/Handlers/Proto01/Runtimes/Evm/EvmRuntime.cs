using System.Text.Json;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class EvmRuntime : IEvmRuntime
{
    public string RuntimeId => "1";

    #region precompiles with known ABI
    public string NullAddress => "0x0000000000000000000000000000000000000000";

    public string XtzBridge => "0xff00000000000000000000000000000000000001";

    public string FaBridge => "0xff00000000000000000000000000000000000002";

    public string Outbox => "0xff00000000000000000000000000000000000003";

    public string TicketTable => "0xff00000000000000000000000000000000000004";

    public string GlobalCounter => "0xff00000000000000000000000000000000000005";

    public string SequencerUpdater => "0xff00000000000000000000000000000000000006";

    public virtual string MichelsonGateway => throw new NotImplementedException();

    public virtual string AliasForwarder => throw new NotImplementedException();

    public virtual string VerifyTezosSignature => throw new NotImplementedException();
    #endregion

    #region special addresses
    public string DeadAddress => "0x000000000000000000000000000000000000dead";

    public virtual string TezosXCaller => throw new NotImplementedException();

    public string DepositOrigin => "0x000000000000000000000000000000000000feed";
    #endregion

    #region known topics
    public virtual string CracReceivedTopic => throw new NotImplementedException();

    public virtual string CracSentTopic => throw new NotImplementedException();

    public virtual string AliasInitializedTopic => throw new NotImplementedException();

    public virtual string AliasForwardedTopic => throw new NotImplementedException();
    #endregion

    #region known selectors
    public virtual string CallMichelsonSelector => throw new NotImplementedException();

    public virtual string CallSelector => throw new NotImplementedException();

    public byte[] FaClaimSelector { get; } = Selector("claim(uint256)");

    public byte[] XtzClaimSelector { get; } = Selector("claim_xtz(uint256)");
    #endregion

    #region helpers
    public virtual string GetAlias(string address) => throw new NotImplementedException();

    public virtual int ConvertGas(long michelsonMilligas) => throw new NotImplementedException();

    public virtual bool IsCracCall(string? to, JsonElement trace) => throw new NotImplementedException();
    #endregion

    #region utils
    static byte[] Selector(string signature) => Keccak256.GetHashBytes(Utf8.GetBytes(signature))[..4];
    #endregion
}
