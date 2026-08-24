using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

public class DelayedEvmDepositOperation(IDelayedTransaction deposit, EvmOperation feederCall) : IMetaOperationContent
{
    public IDelayedTransaction Deposit { get; } = deposit;
    public EvmOperation FeederCall { get; } = feederCall;
}
