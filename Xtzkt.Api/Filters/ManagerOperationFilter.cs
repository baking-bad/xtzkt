using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class ManagerOperationFilter : BaseOperationFilter
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
    /// Filters by sender address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?sender=tz1...`, `?sender.in=tz1...,0x...`.
    /// </summary>
    public AddressInfoParameter? Sender { get; set; }

    /// <summary>
    /// Filters by the sender's operation counter.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?counter=42`.
    /// </summary>
    public Int32Parameter? Counter { get; set; }

    /// <summary>
    /// Filters by operation status.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?status=applied`, `?status.ne=applied`.
    /// </summary>
    public OperationStatusParameter? Status { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Hash == null &&
        Sender == null &&
        Counter == null &&
        Status == null;

    public override string Normalize(string name) =>
        base.Normalize(name) + ResponseCacheService.BuildKey("",
            ($"{name}.hash", Hash),
            ($"{name}.sender", Sender),
            ($"{name}.counter", Counter),
            ($"{name}.status", Status));
}
