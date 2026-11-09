using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto11;

class ProtoActivator(ProtocolHandler proto) : Proto10.ProtoActivator(proto)
{
    #region evm
    public new const string MichelsonGatewayAbi = "Protocols/Handlers/Proto11/Runtimes/Evm/Precompiles/MichelsonGatewayAbi.json";
    public const string RescueGuardAbi = "Protocols/Handlers/Proto11/Runtimes/Evm/Precompiles/RescueGuardAbi.json";

    protected override List<(string Address, string AbiPath)> EvmPrecompiles => [
        (EvmRuntime.NullAddress,            NullAddressAbi),
        (EvmRuntime.XtzBridge,              XtzBridgeAbi),
        (EvmRuntime.FaBridge,               Proto08.ProtoActivator.FaBridgeAbi),
        (EvmRuntime.Outbox,                 Proto06.ProtoActivator.OutboxAbi),
        (EvmRuntime.TicketTable,            Proto07.ProtoActivator.TicketTableAbi),
        (EvmRuntime.GlobalCounter,          Proto06.ProtoActivator.GlobalCounterAbi),
        (EvmRuntime.SequencerUpdater,       Proto06.ProtoActivator.SequencerUpdaterAbi),
        (EvmRuntime.MichelsonGateway,       MichelsonGatewayAbi), // new
        (EvmRuntime.AliasForwarder,         AliasForwarderAbi),
        (EvmRuntime.VerifyTezosSignature,   VerifyTezosSignatureAbi),
        (EvmRuntime.RescueGuard,            RescueGuardAbi), // new
    ];
    #endregion

    #region michelson
    public override async Task ActivateMichelsonContext(XChain state, MetaBlock block)
    {
        await base.ActivateMichelsonContext(state, block);
        await new RescueClones(Proto).Bind(state);
    }

    public override async Task DeactivateMichelsonContext(XChain state)
    {
        await new RescueClones(Proto).Unbind(state, keepOwners: false);
        await base.DeactivateMichelsonContext(state);
    }
    #endregion
}
