namespace Xtzkt.Indexers.TezosX.Protocols.Proto07;

class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
{
    public const string TicketTableAbi = "Protocols/Handlers/Proto07/Runtimes/Evm/Precompiles/TicketTableAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress,        Proto06.ProtoActivator.NullAddressAbi),
        (EvmRuntime.XtzBridge,          Proto03.ProtoActivator.XtzBridgeAbi),
        (EvmRuntime.FaBridge,           Proto06.ProtoActivator.FaBridgeAbi),
        (EvmRuntime.Outbox,             Proto06.ProtoActivator.OutboxAbi),
        (EvmRuntime.TicketTable,        TicketTableAbi),
        (EvmRuntime.GlobalCounter,      Proto06.ProtoActivator.GlobalCounterAbi),
        (EvmRuntime.SequencerUpdater,   Proto06.ProtoActivator.SequencerUpdaterAbi),
    ];
}
