using System.Numerics;

namespace Xtzkt.Indexers.TezosX.Protocols.Models;

public sealed class Blueprint
{
    public required string SmartRollup { get; init; }
    public required int Level { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Predecessor { get; init; }
    public required List<string> Transactions { get; init; }
    public required List<DelayedOperation> DelayedTransactions { get; init; }
    //public string? KernelUpgrade { get; init; }
    //public DateTime? KernelUpgradeTime { get; init; }
    //public string? SequencerUpgrade { get; init; }
    //public DateTime? SequencerUpgradeTime { get; init; }
}

public sealed class DelayedXtzDeposit : DelayedOperation
{
    public BigInteger Amount { get; init; }
    public required string Receiver { get; init; }
    public int InboxLevel { get; init; }
    public int InboxMessageId { get; init; }
}

public sealed class DelayedFaDeposit : DelayedOperation
{
    public BigInteger Amount { get; init; }
    public required string Receiver { get; init; }
    public string? Proxy { get; init; }
    public required byte[] TicketHash { get; init; }
    public int InboxLevel { get; init; }
    public int InboxMessageId { get; init; }
}

public sealed class DelayedEvmTransaction : DelayedOperation
{
}

public sealed class DelayedMichelsonOperation : DelayedOperation
{
}

public abstract class DelayedOperation
{
    public required string Hash { get; init; }
}
