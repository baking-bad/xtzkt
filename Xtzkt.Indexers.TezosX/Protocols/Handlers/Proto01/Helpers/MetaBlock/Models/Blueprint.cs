using System.Numerics;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

public class Blueprint
{
    public required string SmartRollup { get; init; }
    public required int Level { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Predecessor { get; init; }
    public required List<string> Transactions { get; init; }
    public required List<IDelayedTransaction> DelayedTransactions { get; init; }
    public string? KernelUpgrade { get; init; }
    public DateTime? KernelUpgradeTime { get; init; }
    public string? SequencerUpgrade { get; init; }
    public DateTime? SequencerUpgradeTime { get; init; }
}

public class DelayedDeposit : IDelayedTransaction
{
    public required string Hash { get; init; }
    public BigInteger Amount { get; init; }
    public required string Receiver { get; init; }
    public int InboxLevel { get; init; }
    public int InboxMessageId { get; init; }
}


public class DelayedEvmTransaction : IDelayedTransaction
{
    public required string Hash { get; init; }
}

