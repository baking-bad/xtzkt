using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class TokensCommit(ProtocolHandler protocol) : Proto01.TokensCommit(protocol)
{
    protected override async Task<XAddress> GetCachedOrCreateXAddress(string hash)
    {
        if (!Cache.Addresses.TryGetCached(hash, out var address))
            address = await Helpers.CreateXEvmUser(hash);
        return address;
    }
}
