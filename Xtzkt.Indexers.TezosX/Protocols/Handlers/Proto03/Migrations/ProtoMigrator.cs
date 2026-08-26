using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto03;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override async Task ApplyMigrations(XChain state, MetaBlock block)
    {
        // withdrawal events moved from the system address to the emitting precompiles,
        // and the xtz bridge gained the fast withdrawal entrypoint
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.NullAddress, "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/NullAbi.json", state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.XtzBridge, "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/XtzBridgeAbi.json", state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.FaBridge, "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/FaBridgeAbi.json", state);
    }

    protected override Task RevertMigrations(XChain state)
    {
        throw new NotImplementedException();
    }
}
