using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;

partial class ProtoHelpers
{
    public virtual async Task<MetaBlock> GetMetaBlock(int level)
    {
        var t1 = GetBlueprint(level);
        var t2 = EvmRpc.GetBlockData(level);

        await Task.WhenAll(t1, t2);

        var blueprint = t1.Result;
        var (evmBlock, evmReceipts, evmTraces) = t2.Result;
        var evmReceiptsDict = evmReceipts.EnumerateArray().ToDictionary(x => x.RequiredString("transactionHash"));
        var evmTracesDict = evmTraces.EnumerateArray().ToDictionary(x => x.RequiredString("txHash"), x => x.Required("result"));

        if (evmBlock.RequiredString("parentHash") != blueprint.Predecessor)
            throw new Exception("Inconsistent evm inputs");

        if (evmBlock.RequiredHexTimestamp32("timestamp") != blueprint.Timestamp)
            throw new Exception("Inconsistent evm inputs");

        var _queuesByHash = new Dictionary<string, Queue<MetaContent>>();

        foreach (var tx in evmBlock.RequiredArray("transactions").EnumerateArray())
        {
            var hash = tx.RequiredString("hash");
            var receipt = evmReceiptsDict[hash];
            var trace = evmTracesDict[hash];
            var batch = new EvmBatch
            {
                Hash = hash,
                Index = receipt.RequiredHexInt32("transactionIndex"),
            };
            var op = GetEvmOperation(batch, tx, receipt, trace);

            var queue = new Queue<MetaContent>();
            queue.Enqueue(op);
            foreach (var internalOp in GetEvmInternalOperations(op))
                queue.Enqueue(internalOp);

            _queuesByHash.Add(batch.Hash, queue);
        }

        var batches = new List<MetaBatch>();
        var context = new MetaContext
        {
            DelayedOps = blueprint.DelayedTransactions,
            QueuesByHash = _queuesByHash,
        };

        foreach (var hash in blueprint.DelayedTransactions.Select(x => x.Hash))
        {
            if (TryReadOperation(context, hash, true) is not MetaBatch batch)
            {
                Logger.LogWarning("Operation {hash} was dropped from block {level}", hash, level);
                continue;
            }

            if (blueprint.Transactions.Any(x => x == hash))
                throw new Exception($"Operation {hash} is ambiguous, because included in both Transactions and DelayedTransactions. Cannot proceed.");

            batches.Add(batch);
        }

        foreach (var hash in blueprint.Transactions)
        {
            if (TryReadOperation(context, hash, false) is not MetaBatch batch)
            {
                Logger.LogWarning("Operation {hash} was dropped from block {level}", hash, level);
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
            Hash = evmBlock.RequiredHexBytes("hash"),
            Batches = batches,
            EvmBlock = evmBlock,
            MichelsonBlock = null,
            KernelUpgrade = blueprint.KernelUpgrade,
            KernelUpgradeTime = blueprint.KernelUpgradeTime,
        };
    }

    protected virtual EvmOperation GetEvmOperation(EvmBatch batch, JsonElement tx, JsonElement receipt, JsonElement trace)
    {
        return new EvmOperation
        {
            Batch = batch,
            Tx = tx,
            Receipt = receipt,
            Trace = trace,
            Logs = receipt.OptionalArray("logs")?.EnumerateArray().ToList() ?? [],
            From = receipt.RequiredString("from"),
            To = receipt.OptionalString("to"),
        };
    }

    protected virtual List<EvmInternalOperation> GetEvmInternalOperations(EvmOperation op)
    {
        return [];
    }
}
