using System.Numerics;
using System.Text.Json;

namespace Xtzkt.Indexers.TezosX.Protocols;

public interface IEvmRpc
{
    Task<(JsonElement block, JsonElement receipts, JsonElement traces)> GetBlockData(int level);
    Task<JsonElement> GetBlueprint(int level);
    Task<JsonElement> GetMichelsonActivationLevel();
    Task<JsonElement> GetBalance(string address, int level);
    Task<JsonElement> GetTransactionCount(string address, int level);
    Task<JsonElement> GetBalanceEarliest(string address);
    Task<JsonElement> GetCodeEarliest(string address);

    Task<BigInteger[]> DebugBalances(IEnumerable<string> addresses, int level);
}
