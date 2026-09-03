using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols.Abstract;

public interface IEvmRuntime
{
    string RuntimeId { get; }

    #region precompiles with known ABI
    string NullAddress { get; }
    string XtzBridge { get; }
    string FaBridge { get; }
    string Outbox { get; }
    string TicketTable { get; }
    string GlobalCounter { get; }
    string SequencerUpdater { get; }
    string MichelsonGateway { get; }
    string AliasForwarder { get; }
    string VerifyTezosSignature { get; }
    #endregion

    #region special addresses
    string DeadAddress { get; }
    string TezosXCaller { get; }
    string DepositOrigin { get; }
    #endregion

    #region known topics
    string CracReceivedTopic { get; }
    string CracSentTopic { get; }
    string AliasInitializedTopic { get; }
    string AliasForwardedTopic { get; }
    #endregion

    #region known selectors
    string CallMichelsonSelector { get; }
    string CallSelector { get; }
    byte[] FaClaimSelector { get; }
    byte[] XtzClaimSelector { get; }
    #endregion

    #region helpers
    string GetAlias(string address);
    int ConvertGas(long michelsonMilligas);
    bool IsCracCall(string? to, JsonElement trace);
    #endregion
}
