using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

public class DelayedEvmDepositOperation(IDelayedTransaction deposit, EvmOperation feederCall, IReadOnlyList<EvmInternalOperation> bridgeCalls) : IMetaOperationContent
{
    public IDelayedTransaction Deposit { get; } = deposit;
    public EvmOperation FeederCall { get; } = feederCall;
    public IReadOnlyList<EvmInternalOperation> BridgeCalls { get; } = bridgeCalls;

    public override string ToString()
    {
        return $"Deposit -> {(Deposit as DelayedDeposit)?.Receiver ?? (Deposit as DelayedFaDeposit)!.Receiver}";
    }
}

public class DelayedMichelsonDepositOperation(IDelayedTransaction deposit, MichelsonOperation feederCall, IReadOnlyList<MichelsonInternalOperation> bridgeCalls) : IMetaOperationContent
{
    public IDelayedTransaction Deposit { get; } = deposit;
    public MichelsonOperation FeederCall { get; } = feederCall;
    public IReadOnlyList<MichelsonInternalOperation> BridgeCalls { get; } = bridgeCalls;

    public override string ToString()
    {
        return $"Deposit -> {(Deposit as DelayedDeposit)?.Receiver ?? (Deposit as DelayedFaDeposit)!.Receiver}";
    }
}
