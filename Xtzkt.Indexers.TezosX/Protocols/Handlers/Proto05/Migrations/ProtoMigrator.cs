using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto05;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override Task ApplyMigrations(XChain state, MetaBlock block)
    {
        return Helpers.UpgradeEvmPrecompile(EvmRuntime.FaBridge, [], "Protocols/Handlers/Proto05/Runtimes/Evm/Precompiles/FaBridgeAbi.json", state);
    }

    protected override Task RevertMigrations(XChain state)
    {
        throw new NotImplementedException();
    }
}
