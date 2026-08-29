using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto06;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override async Task ApplyMigrations(XChain state, MetaBlock block)
    {
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.NullAddress, ProtoActivator.NullAddressAbi, state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.XtzBridge, Proto03.ProtoActivator.XtzBridgeAbi, state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.FaBridge, ProtoActivator.FaBridgeAbi, state);

        var nullAddress = await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress;
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.Outbox, ProtoActivator.OutboxAbi, nullAddress, state);
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.TicketTable, ProtoActivator.TicketTableAbi, nullAddress, state);
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.GlobalCounter, ProtoActivator.GlobalCounterAbi, nullAddress, state);
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.SequencerUpdater, ProtoActivator.SequencerUpdaterAbi, nullAddress, state);
    }

    protected override async Task RevertMigrations(XChain state)
    {
        await Helpers.RemoveEvmPrecompile(EvmRuntime.SequencerUpdater, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.GlobalCounter, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.TicketTable, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.Outbox, state);

        await Helpers.DowngradeEvmPrecompile(EvmRuntime.FaBridge, state);
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.XtzBridge, state);
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.NullAddress, state);
    }
}
