using System.Numerics;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

public class Blueprint
{
    public required string SmartRollup { get; init; }
    public required int Level { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Predecessor { get; init; }
    public required List<string> Transactions { get; init; }
    public required List<DelayedTransaction> DelayedTransactions { get; init; }
    public string? KernelUpgrade { get; init; }
    public DateTime? KernelUpgradeTime { get; init; }
    public string? SequencerUpgrade { get; init; }
    public DateTime? SequencerUpgradeTime { get; init; }
}

public abstract class DelayedTransaction
{
    public required string Hash { get; init; }
}

public class DelayedDeposit : DelayedTransaction
{
    public BigInteger Amount { get; init; }
    public required string Receiver { get; init; }
    public int InboxLevel { get; init; }
    public int InboxMessageId { get; init; }
}

public class DelayedFaDeposit : DelayedTransaction
{
    public BigInteger Amount { get; init; }
    public required string Receiver { get; init; }
    public string? Proxy { get; init; }
    public required byte[] TicketHash { get; init; }
    public int InboxLevel { get; init; }
    public int InboxMessageId { get; init; }
}

public class DelayedEvmTransaction : DelayedTransaction { }

public class DelayedMichelsonOperation : DelayedTransaction { }
