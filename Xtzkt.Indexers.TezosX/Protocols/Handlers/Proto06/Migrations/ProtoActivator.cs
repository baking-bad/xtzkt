namespace Xtzkt.Indexers.TezosX.Protocols.Proto06;

class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
{
    public new const string NullAddressAbi = "Protocols/Handlers/Proto06/Runtimes/Evm/Precompiles/NullAddressAbi.json";
    public const string FaBridgeAbi = "Protocols/Handlers/Proto06/Runtimes/Evm/Precompiles/FaBridgeAbi.json";
    public const string OutboxAbi = "Protocols/Handlers/Proto06/Runtimes/Evm/Precompiles/OutboxAbi.json";
    public const string TicketTableAbi = "Protocols/Handlers/Proto06/Runtimes/Evm/Precompiles/TicketTableAbi.json";
    public const string GlobalCounterAbi = "Protocols/Handlers/Proto06/Runtimes/Evm/Precompiles/GlobalCounterAbi.json";
    public const string SequencerUpdaterAbi = "Protocols/Handlers/Proto06/Runtimes/Evm/Precompiles/SequencerUpdaterAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress,        NullAddressAbi),
        (EvmRuntime.XtzBridge,          Proto03.ProtoActivator.XtzBridgeAbi),
        (EvmRuntime.FaBridge,           FaBridgeAbi),
        (EvmRuntime.Outbox,             OutboxAbi),
        (EvmRuntime.TicketTable,        TicketTableAbi),
        (EvmRuntime.GlobalCounter,      GlobalCounterAbi),
        (EvmRuntime.SequencerUpdater,   SequencerUpdaterAbi),
    ];
}
