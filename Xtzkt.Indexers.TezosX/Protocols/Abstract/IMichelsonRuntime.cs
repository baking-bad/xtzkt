using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols.Abstract;

public interface IMichelsonRuntime
{
    string RuntimeId { get; }

    #region special addresses
    string NullAddress { get; }
    string EvmGateway { get; }
    string CracOrigin { get; }
    string DepositOrigin { get; }
    #endregion

    #region helpers
    string GetAlias(string address);
    int ConvertGas(int evmGas);
    bool IsCracCall(string? to, JsonElement content);
    #endregion
}
