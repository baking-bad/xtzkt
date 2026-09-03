using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class LogCommit(ProtocolHandler protocol) : Proto01.LogCommit(protocol)
{
    protected override async Task<XEvmAddress> GetAddress(JsonElement log)
    {
        return (await Cache.Addresses.GetExistingAsync(log.RequiredString("address")) as XEvmAddress)!;
    }
}
