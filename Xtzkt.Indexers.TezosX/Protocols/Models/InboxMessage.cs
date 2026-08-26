using Netezos;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.TezosX.Protocols.Models;

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
        var bytes = Hex.GetBytes(hex);
        FramingProtocol = bytes[0];
        SmartRollupAddress = Netezos.Encoding.Base58.Convert(bytes[1..21], Prefixes.sr1);
        MessageKind = (InboxMessageKind)bytes[21];
        Payload = bytes[22..];
    }
}

public class BlueprintChunk
{
    public required byte[] Chunk { get; init; }
    public required int Level { get; init; }
    public required int ChunksCount { get; init; }
    public required int ChunkIndex { get; init; }
}
