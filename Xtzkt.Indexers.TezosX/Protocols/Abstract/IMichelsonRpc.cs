using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols;

public interface IMichelsonRpc
{
    Task<JsonElement> GetBlockAsync(int level);
    Task<JsonElement> GetContractAsync(int level, string address);
    Task<JsonElement> GetConstantsAsync(int level);
}
