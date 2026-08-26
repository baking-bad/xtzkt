using System.Text.Json;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class MichelsonRuntime : IMichelsonRuntime
{
    public string RuntimeId => "0";

    #region special addresses
    public string NullAddress => throw new NotImplementedException();

    public string EvmGateway => throw new NotImplementedException();

    public string CracOrigin => throw new NotImplementedException();

    public string DepositOrigin => throw new NotImplementedException();
    #endregion

    #region helpers
    public string GetAlias(string address) => throw new NotImplementedException();

    public int ConvertGas(int evmGas) => throw new NotImplementedException();

    public bool IsCracCall(string? to, JsonElement content) => throw new NotImplementedException();
    #endregion
}
