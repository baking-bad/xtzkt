using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class MichelsonRpc() : IMichelsonRpc
{
    public Task<long[]> DebugBalances(IEnumerable<string> addresses, int level)
    {
        throw new NotImplementedException();
    }

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

    public Task<JsonElement> GetContractManagerKeyAsync(int level, string address)
    {
        throw new NotImplementedException();
    }

    public Task<JsonElement> GetContractsAsync(int level)
    {
        throw new NotImplementedException();
    }
}
