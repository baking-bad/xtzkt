using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class BlockCommit(ProtocolHandler protocol) : Proto01.BlockCommit(protocol)
{
    protected override string GetSequencerPoolAddress(JsonElement evmBlock)
    {
        return evmBlock.RequiredString("miner");
    }
}
