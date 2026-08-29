using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override async Task ApplyMigrations(XChain state, MetaBlock block)
    {
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.NullAddress, Proto06.ProtoActivator.NullAddressAbi, state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.XtzBridge, Proto03.ProtoActivator.XtzBridgeAbi, state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.FaBridge, ProtoActivator.FaBridgeAbi, state);
    }

    protected override async Task RevertMigrations(XChain state)
    {
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.FaBridge, state);
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.XtzBridge, state);
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.NullAddress, state);
    }
}
