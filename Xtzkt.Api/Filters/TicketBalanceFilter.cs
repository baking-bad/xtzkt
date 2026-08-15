using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class TicketBalanceFilter : INormalizable
{
    /// <summary>
    /// Filters by internal unique id. Within a chain ids grow over time, so sorting by id sorts chronologically.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?id=123`, `?id.in=123,456`.
    /// </summary>
    public Int64Parameter? Id { get; set; }

    /// <summary>
    /// Filters by chain the item belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainInfoParameter? Chain { get; set; }

    /// <summary>
    /// Filters by ticket.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?ticket=123`, `?ticket.ticketer=KT1...`.
    /// </summary>
    public TicketInfoParameter? Ticket { get; set; }

    /// <summary>
    /// Filters by address holding the balance.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?address=tz1...`, `?address.in=tz1...,0x...`.
    /// </summary>
    public AddressInfoParameter? Address { get; set; }

    /// <summary>
    /// Filters by balance amount.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?balance.gt=0`.
    /// </summary>
    public BigIntegerParameter? Balance { get; set; }

    /// <summary>
    /// Filters by level of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by level of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstTimestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? FirstTimestamp { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastTimestamp.lt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? LastTimestamp { get; set; }

    /// <summary>
    /// Filters by number of transfers.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?transfersCount.gt=10`.
    /// </summary>
    public Int32Parameter? TransfersCount { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Ticket == null &&
        Address == null &&
        Balance == null &&
        FirstLevel == null &&
        LastLevel == null &&
        FirstTimestamp == null &&
        LastTimestamp == null &&
        TransfersCount == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.ticket", Ticket),
        ($"{name}.address", Address),
        ($"{name}.balance", Balance),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.lastLevel", LastLevel),
        ($"{name}.firstTimestamp", FirstTimestamp),
        ($"{name}.lastTimestamp", LastTimestamp),
        ($"{name}.transfersCount", TransfersCount));
}
