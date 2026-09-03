using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class MichelsonRpc : IMichelsonRpc
{
    public Task<JsonElement> GetBlockAsync(int level)
    {
        throw new NotImplementedException();
    }

    public Task<JsonElement> GetConstantsAsync(int level)
    {
        throw new NotImplementedException();
    }

    public Task<JsonElement> GetContractAsync(int level, string address)
    {
        throw new NotImplementedException();
    }
}
