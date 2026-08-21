using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class BridgeTicketTransferFilter : INormalizable
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
    /// Filters by level of the block the item is in.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?level=1500000`, `?level.gt=1500000`.
    /// </summary>
    public Int32Parameter? Level { get; set; }

    /// <summary>
    /// Filters by timestamp of the block the item is in.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?timestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? Timestamp { get; set; }

    /// <summary>
    /// Filters by bridge ticket.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?ticket=123`, `?ticket.weakHash=0x...`.
    /// </summary>
    public BridgeTicketInfoParameter? Ticket { get; set; }

    /// <summary>
    /// Matches an address against any of the listed fields (`from`, `to`), instead of just one.
    /// This is how you get everything related to an address in a single request, rather than
    /// querying each field separately and merging the results yourself.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?anyof.from.to=0x...`, `?anyof.from.to.in=0x...,0x...`.
    /// </summary>
    public AnyOfParameter? Anyof { get; set; }

    /// <summary>
    /// Filters by sender address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?from=0x...`, `?from.null=true`.
    /// </summary>
    public AddressInfoNullParameter? From { get; set; }

    /// <summary>
    /// Filters by target address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?to=0x...`, `?to.null=true`.
    /// </summary>
    public AddressInfoNullParameter? To { get; set; }

    /// <summary>
    /// Filters by amount.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?amount.gt=0`.
    /// </summary>
    public BigIntegerParameter? Amount { get; set; }

    /// <summary>
    /// Filters by the transaction operation that caused the transfer.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?transactionId=123`, `?transactionId.null=false`.
    /// </summary>
    public Int64NullParameter? TransactionId { get; set; }

    /// <summary>
    /// Filters by the deposit operation that caused the transfer.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?depositId=123`.
    /// </summary>
    public Int64NullParameter? DepositId { get; set; }

    [JsonIgnore]
    internal OrParameter? Or { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Level == null &&
        Timestamp == null &&
        Ticket == null &&
        Anyof == null &&
        From == null &&
        To == null &&
        Amount == null &&
        TransactionId == null &&
        DepositId == null &&
        Or == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.level", Level),
        ($"{name}.timestamp", Timestamp),
        ($"{name}.ticket", Ticket),
        ($"{name}.anyof", Anyof),
        ($"{name}.from", From),
        ($"{name}.to", To),
        ($"{name}.amount", Amount),
        ($"{name}.transactionId", TransactionId),
        ($"{name}.depositId", DepositId),
        ($"{name}.or", Or));
}
