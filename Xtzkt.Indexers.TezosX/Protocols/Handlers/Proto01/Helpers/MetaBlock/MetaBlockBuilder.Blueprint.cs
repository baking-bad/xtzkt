using Netezos;
using System.Numerics;
using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils;
using Xtzkt.Utils.Crypto;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

public partial class MetaBlockBuilder
{
    protected async Task<Blueprint> GetBlueprint(int level)
    {
        var json = await evmRpc.GetBlueprint(level);

        var delayedTransactions = json.RequiredArray("delayed_transactions")
            .EnumerateArray()
            .Select(x => (DelayedTransaction)(x[0].RequiredString() switch
            {
                "deposit" => ParseDelayedDeposit(x[1].RequiredHexBytes(), x[2].RequiredHexBytes()),
                "fa_deposit" => ParseDelayedFaDeposit(x[1].RequiredHexBytes(), x[2].RequiredHexBytes()),
                "transaction" => ParseDelayedEvmTransaction(x[1].RequiredHexBytes()),
                "operation" => ParseDelayedMichelsonOperation(x[1].RequiredHexBytes()),
                _ => throw new FormatException("Invalid delayed transactions format"),
            }))
            .ToList();

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
        if (stream.CanRead || rlp as RlpList is not [.. var v, RlpItem r1, RlpList r2, RlpList r3, RlpItem r4])
            throw new FormatException("Invalid Blueprint format");

        if (v is not [] and [RlpItem { Data: not [1] }])
            throw new NotSupportedException("Not supported Blueprint version");

        if (DateTime.UnixEpoch.AddSeconds(HexNumber.GetInt64Reverse(r4.Data)) != timestamp)
            throw new Exception("Inconsistent timestamp");

        var predecessor = Hex.Convert(r1.Data);
        var delayedTransactionsHashes = r2.Select(x => Hex.Convert((x as RlpItem)?.Data ?? throw new Exception("Invalid Blueprint.DelayedTransactions"))).ToList();
        var transactionsHashes = r3.Select(x => GetTransactionHash((x as RlpItem)?.Data ?? throw new Exception("Invalid Blueprint.Transactions"))).ToList();

        if (delayedTransactions.Count != delayedTransactionsHashes.Count)
            throw new Exception("Inconsistent delayed transactions");

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

    public static DelayedDeposit ParseDelayedDeposit(byte[] hash, byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        if (stream.Read() is not RlpList rlp || stream.CanRead)
            throw new FormatException("Invalid delayed deposit rlp");

        if (rlp is [RlpItem e0, RlpItem e1, RlpItem e2, RlpItem e3])
        {
            return new DelayedDeposit
            {
                Hash = Hex.Convert(hash),
                Amount = new BigInteger(e0.Data, true, true),
                Receiver = Hex.Convert(e1.Data),
                InboxLevel = HexNumber.GetInt32(e2.Data),
                InboxMessageId = HexNumber.GetInt32(e3.Data),
            };
        }
        else if (rlp is [RlpItem m0, RlpList ml, RlpItem m2, RlpItem m3] && ml is [RlpItem { Data: [1] }, RlpItem m1])
        {
            return new DelayedDeposit
            {
                Hash = Netezos.Encoding.Base58.Convert(hash, Prefixes.o),
                Amount = new BigInteger(m0.Data, true, true),
                Receiver = (m1.Data[0], m1.Data[1]) switch
                {
                    (0, 0) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz1),
                    (0, 1) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz2),
                    (0, 2) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz3),
                    (0, 3) => Netezos.Encoding.Base58.Convert(m1.Data[2..], Prefixes.tz4),
                    (1, _) when m1.Data[^1] == 0 => Netezos.Encoding.Base58.Convert(m1.Data[1..^1], Prefixes.KT1),
                    _ => throw new FormatException("Invalid Tezos address"),
                },
                InboxLevel = HexNumber.GetInt32(m2.Data),
                InboxMessageId = HexNumber.GetInt32(m3.Data),
            };
        }
        throw new FormatException("Invalid delayed deposit rlp");
    }

    public static DelayedFaDeposit ParseDelayedFaDeposit(byte[] hash, byte[] bytes)
    {
        var stream = new RlpStream(bytes);
        if (stream.Read() is not RlpList rlp || stream.CanRead)
            throw new FormatException("Invalid delayed fa deposit rlp");

        if (rlp is [RlpItem e0, RlpItem e1, RlpList e2, RlpItem e3, RlpItem e4, RlpItem e5])
        {
            return new DelayedFaDeposit
            {
                Hash = Hex.Convert(hash),
                Amount = new BigInteger(e0.Data, true, true),
                Receiver = Hex.Convert(e1.Data),
                Proxy = e2 is [RlpItem _e2]
                    ? Hex.Convert(_e2.Data)
                    : e2 is []
                        ? null
                        : throw new FormatException("Invalid delayed fa deposit rlp"),
                TicketHash = e3.Data,
                InboxLevel = HexNumber.GetInt32(e4.Data),
                InboxMessageId = HexNumber.GetInt32(e5.Data),
            };
        }
        throw new FormatException("Invalid delayed fa deposit rlp");
    }

    public static DelayedEvmTransaction ParseDelayedEvmTransaction(byte[] hash)
    {
        return new DelayedEvmTransaction
        {
            Hash = Hex.Convert(hash),
        };
    }

    public static DelayedMichelsonOperation ParseDelayedMichelsonOperation(byte[] hash)
    {
        return new DelayedMichelsonOperation
        {
            Hash = Netezos.Encoding.Base58.Convert(hash, Prefixes.o),
        };
    }

    protected static string GetTransactionHash(byte[] bytes)
    {
        if (bytes[0] == 0)
            return Netezos.Encoding.Base58.Convert(Blake2Fast.Blake2b.ComputeHash(32, bytes.AsSpan(1)), Prefixes.o);

        //TODO: fix it
        //return Keccak256.GetHash(bytes);
        return Keccak256.GetHash(bytes[1..]);
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