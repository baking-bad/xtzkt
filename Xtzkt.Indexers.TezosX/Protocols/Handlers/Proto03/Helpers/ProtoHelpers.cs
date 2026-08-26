using Xtzkt.Indexers.TezosX.Protocols.Models;
using Xtzkt.Indexers.TezosX.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto03.Helpers;

class ProtoHelpers(ProtocolHandler protocol) : Proto02.Helpers.ProtoHelpers(protocol)
{
    protected override BlueprintChunk ParseChunk(byte[] payload)
    {
        var stream = new RlpStream(payload);
        var rlp = stream.Read();
        // since Calypso a chunk may carry an optional chain_id (u256 LE) right before the signature
        if (stream.CanRead || rlp is not RlpList list || list.Count is not (5 or 6) || list.Any(x => x is not RlpItem))
            throw new FormatException("Invalid BlueprintChunk format");

        return new BlueprintChunk
        {
            Chunk = ((RlpItem)list[0]).Data,
            Level = HexNumber.GetInt32Reverse(((RlpItem)list[1]).Data),
            ChunksCount = HexNumber.GetInt32Reverse(((RlpItem)list[2]).Data),
            ChunkIndex = HexNumber.GetInt32Reverse(((RlpItem)list[3]).Data),
        };
    }
}
