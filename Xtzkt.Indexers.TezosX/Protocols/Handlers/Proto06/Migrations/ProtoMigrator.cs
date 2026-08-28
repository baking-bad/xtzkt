using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto06;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    const string Path = "Protocols/Handlers/Proto06/Runtimes/Evm/Precompiles/";

    protected override async Task ApplyMigrations(XChain state, MetaBlock block)
    {
        #region predeployed precompiles
        var addresses = new[] { EvmRuntime.NullAddress, EvmRuntime.XtzBridge, EvmRuntime.FaBridge };
        var abis = new[] { "NullAbi.json", "XtzBridgeAbi.json", "FaBridgeAbi.json" };

        var codes = (await Proto.EvmRpc.GetCode(addresses, Context.Block.Level))
            .Select(x => x.RequiredHexBytes())
            .ToArray();

        for (int i = 0; i < addresses.Length; i++)
            await Helpers.UpgradeEvmPrecompile(addresses[i], codes[i], Path + abis[i], state);
        #endregion

        #region custom precompiles
        var nullAddress = await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress;
        Helpers.BootstrapEvmPrecompile(EvmRuntime.Outbox, [], Path + "OutboxAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.TicketTable, [], Path + "TicketTableAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.GlobalCounter, [], Path + "GlobalCounterAbi.json", nullAddress, state);
        Helpers.BootstrapEvmPrecompile(EvmRuntime.SequencerUpdater, [], Path + "SequencerUpdaterAbi.json", nullAddress, state);
        #endregion
    }

    protected override Task RevertMigrations(XChain state)
    {
        throw new NotImplementedException();
    }
}
