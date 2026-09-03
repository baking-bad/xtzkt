using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

partial class OriginationCommit(ProtocolHandler protocol) : Proto01.OriginationCommit(protocol)
{
    protected override int GetRootOwnGasUsed(int gasUsed, JsonElement trace, int frameGasOffset)
    {
        // status is taken from the trace to match skipping logic in ProtocolHandler
        var ownGasUsed = gasUsed - SubcallsGasUsed(trace, trace.TraceStatus(), frameGasOffset);
        return ownGasUsed;
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
