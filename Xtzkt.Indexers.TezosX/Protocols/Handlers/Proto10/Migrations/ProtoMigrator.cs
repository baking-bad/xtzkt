using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class ProtoMigrator(ProtocolHandler proto) : Proto01.ProtoMigrator(proto)
{
    protected override async Task ApplyMigrations(XChain state, MetaBlock block)
    {
        Context.Protocol.HardEvmBlockGasLimit = 2L << 50;
        Context.Protocol.HardEvmOperationGasLimit = 2L << 50;

        await Helpers.UpgradeEvmPrecompile(EvmRuntime.NullAddress, ProtoActivator.NullAddressAbi, state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.XtzBridge, ProtoActivator.XtzBridgeAbi, state);
        await Helpers.UpgradeEvmPrecompile(EvmRuntime.FaBridge, Proto08.ProtoActivator.FaBridgeAbi, state);

        var nullAddress = await Cache.Addresses.GetExistingAsync(EvmRuntime.NullAddress) as XEvmAddress;
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.MichelsonGateway, ProtoActivator.MichelsonGatewayAbi, nullAddress, state);
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.AliasForwarder, ProtoActivator.AliasForwarderAbi, nullAddress, state);
        await Helpers.BootstrapEvmPrecompile(EvmRuntime.VerifyTezosSignature, ProtoActivator.VerifyTezosSignatureAbi, nullAddress, state);
    }

    protected override async Task RevertMigrations(XChain state)
    {
        await Helpers.RemoveEvmPrecompile(EvmRuntime.VerifyTezosSignature, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.AliasForwarder, state);
        await Helpers.RemoveEvmPrecompile(EvmRuntime.MichelsonGateway, state);

        await Helpers.DowngradeEvmPrecompile(EvmRuntime.FaBridge, state);
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.XtzBridge, state);
        await Helpers.DowngradeEvmPrecompile(EvmRuntime.NullAddress, state);
    }
}
