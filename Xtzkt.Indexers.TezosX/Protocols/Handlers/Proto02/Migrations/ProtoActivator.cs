namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
{
    public new const string NullAddressAbi = "Protocols/Handlers/Proto02/Runtimes/Evm/Precompiles/NullAddressAbi.json";
    public const string FaBridgeAbi = "Protocols/Handlers/Proto02/Runtimes/Evm/Precompiles/FaBridgeAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress, NullAddressAbi),
        (EvmRuntime.XtzBridge,   Proto01.ProtoActivator.XtzBridgeAbi),
        (EvmRuntime.FaBridge,    FaBridgeAbi),
    ];
}
