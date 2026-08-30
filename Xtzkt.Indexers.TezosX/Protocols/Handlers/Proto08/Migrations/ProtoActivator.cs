namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
{
    public const string FaBridgeAbi = "Protocols/Handlers/Proto08/Runtimes/Evm/Precompiles/FaBridgeAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress,        Proto06.ProtoActivator.NullAddressAbi),
        (EvmRuntime.XtzBridge,          Proto03.ProtoActivator.XtzBridgeAbi),
        (EvmRuntime.FaBridge,           FaBridgeAbi),
        (EvmRuntime.Outbox,             Proto06.ProtoActivator.OutboxAbi),
        (EvmRuntime.TicketTable,        Proto07.ProtoActivator.TicketTableAbi),
        (EvmRuntime.GlobalCounter,      Proto06.ProtoActivator.GlobalCounterAbi),
        (EvmRuntime.SequencerUpdater,   Proto06.ProtoActivator.SequencerUpdaterAbi),
    ];
}
