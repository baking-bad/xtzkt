using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class DepositOperationFilter : BaseOperationFilter
{
    /// <summary>
    /// Filters by operation hash.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?hash=o...`, `?hash=0x...`.
    /// </summary>
    public OperationHashParameter? Hash { get; set; }

    /// <summary>
    /// Filters by operation status.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?status=applied`, `?status.ne=applied`.
    /// </summary>
    public OperationStatusParameter? Status { get; set; }

    /// <summary>
    /// Filters by inbox level.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?inboxLevel=1500000`.
    /// </summary>
    public Int32Parameter? InboxLevel { get; set; }

    /// <summary>
    /// Filters by receiver address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?receiver=0x...`.
    /// </summary>
    public AddressInfoParameter? Receiver { get; set; }

    /// <summary>
    /// Filters by proxy address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?proxy=0x...`.
    /// </summary>
    public AddressInfoParameter? Proxy { get; set; }

    /// <summary>
    /// Filters by deposit type (`xtz` or `fa`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?type=xtz`.
    /// </summary>
    public DepositTypeParameter? Type { get; set; }

    /// <summary>
    /// Filters by ticket hash.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?ticketHash=0x...`.
    /// </summary>
    public HexBytesParameter? TicketHash { get; set; }

    /// <summary>
    /// Filters by deposit id.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?depositId=1`, `?depositId.null=false`.
    /// </summary>
    public BigIntegerNullParameter? DepositId { get; set; }

    [JsonIgnore]
    internal OrParameter? Or { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Hash == null &&
        Status == null &&
        InboxLevel == null &&
        Receiver == null &&
        Proxy == null &&
        Type == null &&
        TicketHash == null &&
        DepositId == null &&
        Or == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.hash", Hash),
        ($"{name}.status", Status),
        ($"{name}.inboxLevel", InboxLevel),
        ($"{name}.receiver", Receiver),
        ($"{name}.proxy", Proxy),
        ($"{name}.type", Type),
        ($"{name}.ticketHash", TicketHash),
        ($"{name}.depositId", DepositId),
        ($"{name}.or", Or));
}
