namespace Xtzkt.Indexers.TezosX.Protocols.Proto03;

class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
{
    public new const string NullAddressAbi = "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/NullAddressAbi.json";
    public new const string XtzBridgeAbi = "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/XtzBridgeAbi.json";
    public const string FaBridgeAbi = "Protocols/Handlers/Proto03/Runtimes/Evm/Precompiles/FaBridgeAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress, NullAddressAbi),
        (EvmRuntime.XtzBridge,   XtzBridgeAbi),
        (EvmRuntime.FaBridge,    FaBridgeAbi),
    ];
}
