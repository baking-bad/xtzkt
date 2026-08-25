using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers.MetaBlock;

public class DelayedEvmDepositOperation(IDelayedTransaction deposit, EvmOperation feederCall) : IMetaOperationContent
{
    public IDelayedTransaction Deposit { get; } = deposit;
    public EvmOperation FeederCall { get; } = feederCall;

    public override string ToString()
    {
        return $"Deposit -> {(Deposit as DelayedDeposit)?.Receiver ?? (Deposit as DelayedFaDeposit)!.Receiver}";
    }
}
