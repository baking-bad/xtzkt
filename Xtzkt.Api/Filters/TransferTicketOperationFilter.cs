using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class TransferTicketOperationFilter : ManagerOperationFilter
{
    /// <summary>
    /// Matches an address against any of the listed fields (`sender`, `target`, `ticketer`),
    /// instead of just one. This is how you get everything related to an address in a single request,
    /// rather than querying each field separately and merging the results yourself.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?anyof.sender.target=tz1...`, `?anyof.sender.target.ticketer=KT1...`.
    /// </summary>
    public AnyOfParameter? Anyof { get; set; }

    /// <summary>
    /// Filters by target address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?target=KT1...`.
    /// </summary>
    public AddressInfoParameter? Target { get; set; }

    /// <summary>
    /// Filters by ticketer (contract that issued the ticket).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?ticketer=KT1...`.
    /// </summary>
    public AddressInfoParameter? Ticketer { get; set; }

    /// <summary>
    /// Filters by amount.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?amount.gt=0`.
    /// </summary>
    public BigIntegerParameter? Amount { get; set; }

    /// <summary>
    /// Filters by entrypoint called on the target contract.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?entrypoint=transfer`.
    /// </summary>
    public StringParameter? Entrypoint { get; set; }

    [JsonIgnore]
    internal OrParameter? Or { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Anyof == null &&
        Target == null &&
        Ticketer == null &&
        Amount == null &&
        Entrypoint == null &&
        Or == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.anyof", Anyof),
        ($"{name}.target", Target),
        ($"{name}.ticketer", Ticketer),
        ($"{name}.amount", Amount),
        ($"{name}.entrypoint", Entrypoint),
        ($"{name}.or", Or));
}
