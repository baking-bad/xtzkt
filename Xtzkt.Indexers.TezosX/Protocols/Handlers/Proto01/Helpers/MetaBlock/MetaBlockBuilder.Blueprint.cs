using System.Numerics;
using System.Text.Json;
using Netezos;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Cache;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils.Crypto;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

public partial class MetaBlockBuilder
{
    const int DelayedTransactionsLookback = 64;

    protected async Task<Blueprint> GetBlueprint(int level)
    {
        var json = await evmRpc.GetBlueprint(level);

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
            .Select(x => new BlueprintChunk(x.Payload))
            .ToList();

        if (chunks.Any(x => x.ChunksCount != chunks.Count || x.ChunkIndex >= chunks.Count || x.Level != number))
            throw new Exception("Inconsistent chunks");

        var stream = new RlpStream([.. chunks.OrderBy(x => x.ChunkIndex).SelectMany(x => x.Chunk)]);
        var rlp = stream.Read();
        if (stream.CanRead || rlp as RlpList is not [RlpItem r1, RlpList r2, RlpList r3, RlpItem r4])
            throw new FormatException("Invalid Blueprint format");

        if (DateTime.UnixEpoch.AddSeconds(HexNumber.GetInt64Reverse(r4.Data)) != timestamp)
            throw new Exception("Inconsistent timestamp");

        var predecessor = Hex.Convert(r1.Data);
        var delayedTransactionsHashes = r2.Select(x => Hex.Convert((x as RlpItem)?.Data ?? throw new Exception("Invalid Blueprint.DelayedTransactions"))).ToList();
        var transactionsHashes = r3.Select(x => GetTransactionHash((x as RlpItem)?.Data ?? throw new Exception("Invalid Blueprint.Transactions"))).ToList();

        var delayedTransactions = new List<IDelayedTransaction>(delayedTransactionsHashes.Count);
        foreach (var hash in delayedTransactionsHashes)
            delayedTransactions.Add(await ResolveDelayedTransaction(hash, level));

        string? kernelUpgrade = null;
        DateTime? kernelUpgradeTime = null;
        if (json.TryGetProperty("kernel_upgrade", out var _kernelUpgrade) && _kernelUpgrade.ValueKind != JsonValueKind.Null)
        {
            // the node returns the kernel root hash as raw hex, we store it normalized (`0x` prefixed, lowercase)
            kernelUpgrade = Xtzkt.Utils.Encoding.Hex.GetString(_kernelUpgrade[0].RequiredHexBytes());
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

    void CacheDelayedTransactions(JsonElement json, int level)
    {
        foreach (var x in json.RequiredArray("delayed_transactions").EnumerateArray())
        {
            var hash = x[1].RequiredHexBytes();
            cache.DelayedTransactions.Add(Hex.Convert(hash), new DelayedTransaction(
                level,
                x[0].RequiredString(),
                hash,
                x[2].RequiredHexBytes()));
        }
    }

    async Task<IDelayedTransaction> ResolveDelayedTransaction(string hash, int level)
    {
        if (cache.DelayedTransactions.TryGet(hash, out var cached))
            return ParseDelayedTransaction(cached);

        for (int i = 1; i <= DelayedTransactionsLookback && level - i >= 0; i++)
        {
            logger.LogDebug("Looking up delayed transaction {hash} in blueprint {level}", hash, level - i);
            CacheDelayedTransactions(await evmRpc.GetBlueprint(level - i), level - i);

            if (cache.DelayedTransactions.TryGet(hash, out cached))
                return ParseDelayedTransaction(cached);
        }

        throw new Exception($"Delayed transaction {hash} applied in block {level} wasn't found in the {DelayedTransactionsLookback} preceding blueprints");
    }

    static IDelayedTransaction ParseDelayedTransaction(DelayedTransaction delayedTransaction)
    {
        return delayedTransaction.Kind switch
        {
            "deposit" => ParseDelayedDeposit(delayedTransaction.Hash, delayedTransaction.Payload),
            "transaction" => ParseDelayedEvmTransaction(delayedTransaction.Hash),
            _ => throw new NotSupportedException($"Delayed transaction '{delayedTransaction.Kind}' is not supported by this kernel"),
        };
    }

    public static DelayedDeposit ParseDelayedDeposit(byte[] hash, byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        if (stream.Read() is not RlpList rlp || stream.CanRead)
            throw new FormatException("Invalid delayed deposit rlp");

        if (rlp is [RlpItem e0, RlpItem e1])
        {
            return new DelayedDeposit
            {
                Hash = Hex.Convert(hash),
                Amount = new BigInteger(e0.Data, true, true),
                Receiver = Hex.Convert(e1.Data),
                InboxLevel = 0,
                InboxMessageId = 0,
            };
        }
        throw new FormatException("Invalid delayed deposit rlp");
    }

    public static DelayedEvmTransaction ParseDelayedEvmTransaction(byte[] hash)
    {
        return new DelayedEvmTransaction
        {
            Hash = Hex.Convert(hash),
        };
    }

    protected static string GetTransactionHash(byte[] bytes)
    {
        return Keccak256.GetHash(bytes);
    }
}

public enum InboxMessageKind
{
    Simple_transaction = 0,
    New_chunked_transaction = 1,
    Transaction_chunk = 2,
    Blueprint_chunk = 3,
    Sequencer_signal = 4,
}

public class InboxMessage
{
    public int FramingProtocol { get; }
    public string SmartRollupAddress { get; }
    public InboxMessageKind MessageKind { get; }
    public byte[] Payload { get; }

    public InboxMessage(string hex)
    {
        var bytes = Hex.Parse(hex);
        FramingProtocol = bytes[0];
        SmartRollupAddress = Netezos.Encoding.Base58.Convert(bytes[1..21], Prefixes.sr1);
        MessageKind = (InboxMessageKind)bytes[21];
        Payload = bytes[22..];
    }
}

public class BlueprintChunk
{
    public byte[] Chunk { get; }
    public int Level { get; }
    public int ChunksCount { get; }
    public int ChunkIndex { get; }

    public BlueprintChunk(byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        var rlp = stream.Read();
        if (stream.CanRead || rlp as RlpList is not [RlpItem r0, RlpItem r1, RlpItem r2, RlpItem r3, RlpItem])
            throw new FormatException("Invalid BlueprintChunk format");

        Chunk = r0.Data;
        Level = HexNumber.GetInt32Reverse(r1.Data);
        ChunksCount = HexNumber.GetInt32Reverse(r2.Data);
        ChunkIndex = HexNumber.GetInt32Reverse(r3.Data);
    }
}