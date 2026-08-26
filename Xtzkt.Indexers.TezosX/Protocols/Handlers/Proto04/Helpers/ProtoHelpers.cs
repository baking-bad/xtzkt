using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto04.Helpers;

class ProtoHelpers(ProtocolHandler protocol) : Proto03.Helpers.ProtoHelpers(protocol)
{
    protected override EvmDeposit CreateFaDeposit(EvmOperation feederCall, DelayedFaDeposit faDeposit)
    {
        return new EvmDeposit { Deposit = faDeposit, FeederCall = feederCall };
    }
}
