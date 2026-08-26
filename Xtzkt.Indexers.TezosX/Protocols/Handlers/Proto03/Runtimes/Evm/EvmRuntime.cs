using System.Text.Json;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto03;

public class EvmRuntime : IEvmRuntime
{
    public string RuntimeId => "1";

    #region precompiles with known ABI
    public string NullAddress => "0x0000000000000000000000000000000000000000";

    public string XtzBridge => "0xff00000000000000000000000000000000000001";

    public string FaBridge => "0xff00000000000000000000000000000000000002";

    public string Outbox => throw new NotImplementedException();

    public string TicketTable => throw new NotImplementedException();

    public string GlobalCounter => throw new NotImplementedException();

    public string SequencerUpdater => throw new NotImplementedException();

    public string MichelsonGateway => throw new NotImplementedException();

    public string AliasForwarder => throw new NotImplementedException();

    public string VerifyTezosSignature => throw new NotImplementedException();
    #endregion

    #region special addresses
    public string DeadAddress => "0x000000000000000000000000000000000000dead";

    public string TezosXCaller => throw new NotImplementedException();

    public string DepositOrigin => throw new NotImplementedException();
    #endregion

    #region known topics
    public string CracReceivedTopic => throw new NotImplementedException();

    public string CracSentTopic => throw new NotImplementedException();

    public string AliasInitializedTopic => throw new NotImplementedException();

    public string AliasForwardedTopic => throw new NotImplementedException();
    #endregion

    #region known selectors

    public string CallMichelsonSelector => throw new NotImplementedException();

    public string CallSelector => throw new NotImplementedException();

    public byte[] FaClaimSelector => throw new NotImplementedException();

    public byte[] XtzClaimSelector => throw new NotImplementedException();
    #endregion

    #region helpers
    public string GetAlias(string address) => throw new NotImplementedException();

    public int ConvertGas(long michelsonMilligas) => throw new NotImplementedException();

    public bool IsCracCall(string? to, JsonElement trace) => throw new NotImplementedException();
    #endregion
}
