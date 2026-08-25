using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers.MetaBlock;

public partial class MetaBlockBuilder(IEvmRpc evmRpc, IEvmRuntime evmRuntime, CacheService cache, ILogger logger)
{
    public async Task<IMetaBlock> GetNextBlock(XChain state)
    {
        var level = state.Level + 1;

        var t1 = GetBlueprint(level);
        var t2 = evmRpc.GetBlockData(level);

        await Task.WhenAll(t1, t2);

        var blueprint = t1.Result;
        var (evmBlock, evmReceipts, evmTraces) = t2.Result;
        var evmReceiptsDict = evmReceipts.EnumerateArray().ToDictionary(x => x.RequiredString("transactionHash"));
        var evmTracesDict = evmTraces.EnumerateArray().ToDictionary(x => x.RequiredString("txHash"), x => x.Required("result"));

        if (evmBlock.RequiredString("parentHash") != blueprint.Predecessor)
            throw new Exception("Inconsistent evm inputs");

        if (evmBlock.RequiredHexTimestamp32("timestamp") != blueprint.Timestamp)
            throw new Exception("Inconsistent evm inputs");

        var _queuesByHash = new Dictionary<string, Queue<IMetaContent>>();

        foreach (var tx in evmBlock.RequiredArray("transactions").EnumerateArray())
        {
            var hash = tx.RequiredString("hash");
            var batch = new EvmBatch(tx, evmReceiptsDict[hash], evmTracesDict[hash]);

            var queue = new Queue<IMetaContent>();
            foreach (var op in batch.Operations)
            {
                queue.Enqueue(op);
                foreach (var internalOp in op.Internals)
                    queue.Enqueue(internalOp);
            }

            _queuesByHash.Add(batch.Hash, queue);
        }

        var reader = new MetaBlockReader(evmRuntime, blueprint.DelayedTransactions, _queuesByHash);
        var batches = new List<IMetaBatch>();

        foreach (var hash in blueprint.DelayedTransactions.Select(x => x.Hash))
        {
            if (reader.TryReadOperation(hash, true) is not MetaBatch batch)
            {
                logger.LogWarning("Operation {hash} was dropped from block {level}", hash, level);
                continue;
            }

            if (blueprint.Transactions.Any(x => x == hash))
                throw new Exception($"Operation {hash} is ambiguous, because included in both Transactions and DelayedTransactions. Cannot proceed.");

            batches.Add(batch);
        }

        foreach (var hash in blueprint.Transactions)
        {
            if (reader.TryReadOperation(hash, false) is not MetaBatch batch)
            {
                logger.LogWarning("Operation {hash} was dropped from block {level}", hash, level);
                continue;
            }

            batches.Add(batch);
        }

        if (_queuesByHash.Values.Any(x => x.Count != 0))
            throw new Exception("Not all operations were consumed");

        return new MetaBlock
        {
            Level = blueprint.Level,
            Timestamp = blueprint.Timestamp,
            Hash = evmBlock.RequiredString("hash"),
            Batches = batches,
            Delayed = blueprint.DelayedTransactions,
            EvmBlock = evmBlock,
            MichelsonBlock = null,
            KernelUpgrade = blueprint.KernelUpgrade,
            KernelUpgradeTime = blueprint.KernelUpgradeTime,
        };
    }
}
