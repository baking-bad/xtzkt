using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto05;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override Task ApplyMigrations(XChain state, MetaBlock block)
    {
        return Helpers.UpgradeEvmPrecompile(EvmRuntime.FaBridge, ProtoActivator.FaBridgeAbi, state);
    }

    protected override Task RevertMigrations(XChain state)
    {
        return Helpers.DowngradeEvmPrecompile(EvmRuntime.FaBridge, state);
    }
}
