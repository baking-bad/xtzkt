using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

public partial class MetaBlockBuilder(IEvmRpc evmRpc, IMichelsonRpc tezRpc, ILogger logger)
{
    public async Task<IMetaBlock> GetNextBlock(XChain state)
    {
        var level = state.Level + 1;

        var t1 = GetBlueprint(level);
        var t2 = evmRpc.GetBlockData(level);
        var t3 = level >= state.MichelsonActivationLevel
            ? tezRpc.GetBlockAsync(level)
            : Task.FromResult<JsonElement>(default);

        await Task.WhenAll(t1, t2, t3);

        var blueprint = t1.Result;
        var (evmBlock, evmReceipts, evmTraces) = t2.Result;
        var evmReceiptsDict = evmReceipts.EnumerateArray().ToDictionary(x => x.RequiredString("transactionHash"));
        var evmTracesDict = evmTraces.EnumerateArray().ToDictionary(x => x.RequiredString("txHash"), x => x.Required("result"));
        var michelsonBlock = t3.Result.ValueKind != JsonValueKind.Undefined ? t3.Result : (JsonElement?)null;

        if (evmBlock.RequiredString("parentHash") != blueprint.Predecessor)
            throw new Exception("Inconsistent evm inputs");

        if (evmBlock.RequiredHexTimestamp32("timestamp") != blueprint.Timestamp)
            throw new Exception("Inconsistent evm inputs");

        if (michelsonBlock != null && michelsonBlock.Value.Required("header").RequiredDateTime("timestamp") != blueprint.Timestamp)
            throw new Exception("Inconsistent michlson inputs");

        var _queuesByHash = new Dictionary<string, Queue<IMetaContent>>();
        var _queuesByCracId = new Dictionary<string, Queue<IMetaContent>>();

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

            if (batch.CracId is string cracId)
                _queuesByCracId.Add(cracId, queue);
            else
                _queuesByHash.Add(batch.Hash, queue);
        }

        var michelsonBatches = michelsonBlock?
            .RequiredArray("operations", 4)[3]
            .EnumerateArray()
            .ToList()
            ?? [];

        var index = 0;
        foreach (var batchJson in michelsonBatches)
        {
            var batch = new MichelsonBatch(index++, batchJson);

            var queue = new Queue<IMetaContent>();
            foreach (var op in batch.Operations)
            {
                queue.Enqueue(op);
                foreach (var internalOp in op.Internals)
                    queue.Enqueue(internalOp);
            }

            if (batch.CracId is string cracId)
                _queuesByCracId.Add(cracId, queue);
            else
                _queuesByHash.Add(batch.Hash, queue);
        }

        var reader = new MetaBlockReader(blueprint.DelayedTransactions, _queuesByHash, _queuesByCracId);
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

        if (_queuesByCracId.Values.Any(x => x.Count != 0))
            throw new Exception("Not all crac internals were consumed");

        return new MetaBlock
        {
            Level = blueprint.Level,
            Timestamp = blueprint.Timestamp,
            Hash = evmBlock.RequiredString("hash"),
            Batches = batches,
            Delayed = blueprint.DelayedTransactions,
            EvmBlock = evmBlock,
            MichelsonBlock = michelsonBlock,
            KernelUpgrade = blueprint.KernelUpgrade,
            KernelUpgradeTime = blueprint.KernelUpgradeTime,
        };
    }
}
