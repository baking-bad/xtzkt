using System.Numerics;
using System.Text.Json;
using Xtzkt.Indexers.Common.Cache;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils.Crypto;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;

partial class ProtoHelpers
{
    protected async Task<Blueprint> GetBlueprint(int level)
    {
        var json = await EvmRpc.GetBlueprint(level);

        CacheDelayedTransactions(json, level);

        var blueprint = json.Required("blueprint");
        var number = blueprint.RequiredInt32("number");
        var timestamp = blueprint.RequiredDateTime("timestamp");

        var messages = blueprint.RequiredArray("payload")
            .EnumerateArray()
            .Select(x => new InboxMessage(x.RequiredString()))
            .ToList();

        if (messages.Any(x => x.FramingProtocol != 0))
            throw new NotSupportedException("Not supported framing protocol");

        if (messages.Any(x => x.MessageKind != InboxMessageKind.Blueprint_chunk))
            throw new NotSupportedException("Not supported message kind");

        var smartRollup = messages[0].SmartRollupAddress;
        if (messages.Any(x => x.SmartRollupAddress != smartRollup))
            throw new NotSupportedException("Multiple rollup addresses is not supported");

        var chunks = messages
            .Select(x => ParseChunk(x.Payload))
            .ToList();

        if (chunks.Any(x => x.ChunksCount != chunks.Count || x.ChunkIndex >= chunks.Count || x.Level != number))
            throw new Exception("Inconsistent chunks");

        var stream = new RlpStream([.. chunks.OrderBy(x => x.ChunkIndex).SelectMany(x => x.Chunk)]);
        var rlp = stream.Read();
        if (stream.CanRead || rlp as RlpList is not [.. var v, RlpItem r1, RlpList r2, RlpList r3, RlpItem r4])
            throw new FormatException("Invalid Blueprint format");

        if (v is not ([] or [RlpItem { Data: [1] }]))
            throw new NotSupportedException("Not supported Blueprint version");

        if (DateTime.UnixEpoch.AddSeconds(HexNumber.GetInt64Reverse(r4.Data)) != timestamp)
            throw new Exception("Inconsistent timestamp");

        var predecessor = Hex.GetString(r1.Data);
        var delayedTransactionsHashes = r2.Select(x => Hex.GetString((x as RlpItem)?.Data ?? throw new Exception("Invalid Blueprint.DelayedTransactions"))).ToList();
        var transactionsHashes = r3.Select(x => GetTransactionHash((x as RlpItem)?.Data ?? throw new Exception("Invalid Blueprint.Transactions"))).ToList();

        var delayedTransactions = new List<DelayedOperation>(delayedTransactionsHashes.Count);
        foreach (var hash in delayedTransactionsHashes)
            delayedTransactions.Add(await ResolveDelayedTransaction(hash, level));

        string? kernelUpgrade = null;
        DateTime? kernelUpgradeTime = null;
        if (json.TryGetProperty("kernel_upgrade", out var _kernelUpgrade) && _kernelUpgrade.ValueKind != JsonValueKind.Null)
        {
            // the node returns the kernel root hash as raw hex, we store it normalized (`0x` prefixed, lowercase)
            kernelUpgrade = Hex.GetString(_kernelUpgrade[0].RequiredHexBytes());
            kernelUpgradeTime = _kernelUpgrade[1].RequiredDateTime();
        }

        string? sequencerUpgrade = null;
        DateTime? sequencerUpgradeTime = null;
        if (json.TryGetProperty("sequencer_upgrade", out var _sequencerUpgrade) && _sequencerUpgrade.ValueKind != JsonValueKind.Null)
        {
            sequencerUpgrade = _sequencerUpgrade[1].RequiredString();
            sequencerUpgradeTime = _sequencerUpgrade[2].RequiredDateTime();
        }

        return new Blueprint
        {
            SmartRollup = smartRollup,

            Level = level,
            Timestamp = timestamp,
            Predecessor = predecessor,

            DelayedTransactions = delayedTransactions,
            Transactions = transactionsHashes,

            KernelUpgrade = kernelUpgrade,
            KernelUpgradeTime = kernelUpgradeTime,

            SequencerUpgrade = sequencerUpgrade,
            SequencerUpgradeTime = sequencerUpgradeTime,
        };
    }

    protected virtual BlueprintChunk ParseChunk(byte[] payload)
    {
        var stream = new RlpStream(payload);
        var rlp = stream.Read();
        if (stream.CanRead || rlp as RlpList is not [RlpItem r0, RlpItem r1, RlpItem r2, RlpItem r3, RlpItem])
            throw new FormatException("Invalid BlueprintChunk format");

        return new BlueprintChunk
        {
            Chunk = r0.Data,
            Level = HexNumber.GetInt32Reverse(r1.Data),
            ChunksCount = HexNumber.GetInt32Reverse(r2.Data),
            ChunkIndex = HexNumber.GetInt32Reverse(r3.Data),
        };
    }

    protected void CacheDelayedTransactions(JsonElement json, int level)
    {
        foreach (var x in json.RequiredArray("delayed_transactions").EnumerateArray())
        {
            var hash = x[1].RequiredHexBytes();
            Cache.DelayedTransactions.Add(Hex.GetString(hash), new DelayedTransaction(
                level,
                x[0].RequiredString(),
                hash,
                x[2].RequiredHexBytes()));
        }
    }

    protected async Task<DelayedOperation> ResolveDelayedTransaction(string hash, int level)
    {
        const int Lookback = 64;

        if (Cache.DelayedTransactions.TryGet(hash, out var cached))
            return ParseDelayedOperation(cached);

        for (int i = 1; i <= Lookback && level - i >= 0; i++)
        {
            Logger.LogDebug("Looking up delayed transaction {hash} in blueprint {level}", hash, level - i);
            CacheDelayedTransactions(await EvmRpc.GetBlueprint(level - i), level - i);

            if (Cache.DelayedTransactions.TryGet(hash, out cached))
                return ParseDelayedOperation(cached);
        }

        throw new Exception($"Delayed transaction {hash} applied in block {level} wasn't found in the {Lookback} preceding blueprints");
    }

    protected virtual DelayedOperation ParseDelayedOperation(DelayedTransaction cached)
    {
        return cached.Kind switch
        {
            "deposit" => ParseDelayedXtzDeposit(cached.Hash, cached.Payload),
            "transaction" => ParseDelayedEvmTransaction(cached.Hash),
            _ => throw new FormatException("Invalid delayed transactions format"),
        };
    }

    protected virtual DelayedXtzDeposit ParseDelayedXtzDeposit(byte[] hash, byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        if (stream.Read() is not RlpList rlp || stream.CanRead)
            throw new FormatException("Invalid delayed deposit rlp");

        if (rlp is [RlpItem e0, RlpItem e1])
        {
            return new DelayedXtzDeposit
            {
                Hash = Hex.GetString(hash),
                Amount = new BigInteger(e0.Data, true, true),
                Receiver = Hex.GetString(e1.Data),
                InboxLevel = 0,
                InboxMessageId = 0,
            };
        }
        throw new FormatException("Invalid delayed deposit rlp");
    }

    protected DelayedEvmTransaction ParseDelayedEvmTransaction(byte[] hash)
    {
        return new DelayedEvmTransaction
        {
            Hash = Hex.GetString(hash),
        };
    }

    protected virtual string GetTransactionHash(byte[] bytes)
    {
        return Keccak256.GetHash(bytes);
    }
}
