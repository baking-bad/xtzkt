using System.Text.Json;
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

    public virtual Task<JsonElement> GetConstantsAsync(int level)
        => Node.GetAsync($"chains/main/blocks/{level}/context/constants");
    #endregion
}
