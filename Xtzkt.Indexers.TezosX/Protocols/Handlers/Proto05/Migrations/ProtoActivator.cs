namespace Xtzkt.Indexers.TezosX.Protocols.Proto05;

class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
{
    public const string FaBridgeAbi = "Protocols/Handlers/Proto05/Runtimes/Evm/Precompiles/FaBridgeAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress, Proto03.ProtoActivator.NullAddressAbi),
        (EvmRuntime.XtzBridge,   Proto03.ProtoActivator.XtzBridgeAbi),
        (EvmRuntime.FaBridge,    FaBridgeAbi),
    ];
}
