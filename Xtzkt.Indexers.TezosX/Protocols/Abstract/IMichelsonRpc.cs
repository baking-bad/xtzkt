using System.Numerics;
using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public interface IMichelsonRpc
    {
        Task<JsonElement> GetBlockAsync(int level);
        Task<JsonElement> GetContractAsync(int level, string address);
        Task<JsonElement> GetContractManagerKeyAsync(int level, string address);
        Task<JsonElement> GetConstantsAsync(int level);
        Task<JsonElement> GetContractsAsync(int level);

        Task<long[]> DebugBalances(IEnumerable<string> addresses, int level);
    }

    public interface IEvmRpc
    {
        Task<(JsonElement block, JsonElement receipts, JsonElement traces)> GetBlockData(int level);
        Task<JsonElement> GetBlueprint(int level);
        Task<JsonElement> GetMichelsonActivationLevel();
        Task<JsonElement> GetBalanceEarliest(string address);
        Task<JsonElement> GetCodeEarliest(string address);

        Task<BigInteger[]> DebugBalances(IEnumerable<string> addresses, int level);
    }
}
