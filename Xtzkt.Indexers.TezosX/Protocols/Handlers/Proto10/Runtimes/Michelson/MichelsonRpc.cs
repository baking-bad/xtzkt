using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class MichelsonRpc(TezosNode node) : IMichelsonRpc
{
    protected readonly TezosNode Node = node;

    #region indexer
    public virtual Task<JsonElement> GetBlockAsync(int level)
        => Node.GetAsync($"chains/main/blocks/{level}");

    public virtual Task<JsonElement> GetContractAsync(int level, string address)
        => Node.GetAsync($"chains/main/blocks/{level}/context/contracts/{address}");

    public virtual Task<JsonElement> GetContractManagerKeyAsync(int level, string address)
        => Node.GetAsync($"chains/main/blocks/{level}/context/contracts/{address}/manager_key");

    public virtual Task<JsonElement> GetConstantsAsync(int level)
        => Node.GetAsync($"chains/main/blocks/{level}/context/constants");

    public virtual Task<JsonElement> GetContractsAsync(int level)
        => Node.GetAsync($"chains/main/blocks/{level}/context/contracts");
    #endregion

    public virtual async Task<long[]> DebugBalances(IEnumerable<string> addresses, int level)
    {
        var res = new long[addresses.Count()];
        var ind = 0;
        
        foreach (var address in addresses)
             res[ind++] = await Node.GetAsync<long>($"chains/main/blocks/{level}/context/contracts/{address}/balance?forward=true");
        
        return res;
    }
}
