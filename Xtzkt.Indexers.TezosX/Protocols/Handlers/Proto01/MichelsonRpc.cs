using System.Numerics;
using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01
{
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

    class EvmRpc(EvmNode node) : IEvmRpc
    {
        static readonly object Tracer = new { tracer = "callTracer", onlyTopCall = false, withLog = true };

        protected readonly EvmNode Node = node;

        public async Task<(JsonElement block, JsonElement receipts, JsonElement traces)> GetBlockData(int level)
        {
            var res = await Node.PostBatchAsync(
                ("eth_getBlockByNumber", [level.ToString(), true]),
                ("eth_getBlockReceipts", [level.ToString()]),
                ("debug_traceBlockByNumber", [level.ToString(), Tracer]));

            return (res[0], res[1], res[2]);
        }

        public Task<JsonElement> GetBlueprint(int level)
        {
            return Node.GetAsync($"evm/v2/blueprint/{level}");
        }

        public Task<JsonElement> GetMichelsonActivationLevel()
        {
            return Node.PostAsync("tez_getMichelsonActivationLevel");
        }

        public Task<JsonElement> GetBalanceEarliest(string address)
        {
            return Node.PostAsync("eth_getBalance", address, "earliest");
        }

        public Task<JsonElement> GetCodeEarliest(string address)
        {
            return Node.PostAsync("eth_getCode", address, "earliest");
        }

        public async Task<BigInteger[]> DebugBalances(IEnumerable<string> addresses, int level)
        {
            return [..(await Node.PostBatchForwardAsync([..addresses.Select(x => ("eth_getBalance", new object[] { x, level.ToString() }))]))
            .Select(x => x.RequiredHexBigInteger())];
        }
    }
}
