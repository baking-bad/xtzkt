using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto05.Helpers;

class ProtoHelpers(ProtocolHandler protocol) : Proto04.Helpers.ProtoHelpers(protocol)
{
    protected override string ExpectedFaDepositTarget(DelayedFaDeposit faDeposit)
    {
        return EvmRuntime.FaBridge;
    }
}
