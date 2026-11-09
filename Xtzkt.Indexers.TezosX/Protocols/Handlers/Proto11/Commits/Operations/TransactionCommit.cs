using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11;

class TransactionCommit(ProtocolHandler protocol) : Proto10.TransactionCommit(protocol)
{
    protected override async Task<(XEvmAddress Sender, XMichelsonAddress Alias)> GetEvmMichelsonParties(string senderAddress, string sourceAddress)
    {
        if (EvmRuntime.GetAliases(sourceAddress).Contains(senderAddress))
        {
            var owner = await Helpers.GetOrCreateXMichelsonAddress(sourceAddress);
            return (await Helpers.GetOrCreateXEvmAlias(senderAddress, owner), owner);
        }

        var sender = await Helpers.GetOrCreateXEvmAddress(senderAddress);
        return (sender, await Helpers.GetOrCreateXMichelsonAlias(MichelsonRuntime.GetAlias(senderAddress), sender));
    }

    protected override async Task<(XMichelsonAddress Sender, XEvmAddress Alias)> GetMichelsonEvmParties(string senderAddress, string fromAddress)
    {
        if (MichelsonRuntime.GetAlias(fromAddress) == senderAddress)
        {
            var owner = await Helpers.GetOrCreateXEvmAddress(fromAddress);
            return (await Helpers.GetOrCreateXMichelsonAlias(senderAddress, owner), owner);
        }

        var sender = await Helpers.GetOrCreateXMichelsonAddress(senderAddress);
        return (sender, await Helpers.GetOrCreateXEvmAlias(EvmRuntime.GetAlias(senderAddress), sender));
    }
}
