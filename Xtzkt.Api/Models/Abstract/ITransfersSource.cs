namespace Xtzkt.Api.Models.Abstract;

/// <summary>
/// An operation that can cause token transfers.
/// </summary>
public interface ITokenTransfersSource
{
    long Id { get; }
    int? TokenTransfers { get; }
}

/// <summary>
/// An operation that can cause ticket transfers.
/// </summary>
public interface ITicketTransfersSource
{
    long Id { get; }
    int? TicketTransfers { get; }
}

/// <summary>
/// An operation that can cause bridge ticket transfers.
/// </summary>
public interface IBridgeTicketTransfersSource
{
    long Id { get; }
    int? BridgeTicketTransfers { get; }
}
