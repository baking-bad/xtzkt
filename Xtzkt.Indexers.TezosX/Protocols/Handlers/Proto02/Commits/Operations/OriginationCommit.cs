using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

partial class OriginationCommit(ProtocolHandler protocol) : Proto01.OriginationCommit(protocol)
{
    protected override (int GasUsed, int OwnGasUsed) GetRootGasUsed(JsonElement receipt, JsonElement trace, int frameGasOffset)
    {
        var gasUsed = receipt.RequiredHexInt32("gasUsed");
        var ownGasUsed = gasUsed - SubcallsGasUsed(trace, frameGasOffset);
        return (gasUsed, ownGasUsed);
    }

    protected override EvmOpCode GetOpCode(JsonElement trace)
    {
        return trace.RequiredEvmOpCode("type");
    }

    protected override Task<byte[]> GetCode(string contractAddress, JsonElement trace)
    {
        return Task.FromResult(trace.OptionalHexBytes("output") ?? []);
    }
}
