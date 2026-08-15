using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class OriginationOperationFilter : ManagerOperationFilter
{
    /// <summary>
    /// Matches an address against any of the listed fields (`sender`, `contract`, `baker`, `initiator`),
    /// instead of just one. This is how you get everything related to an address in a single request,
    /// rather than querying each field separately and merging the results yourself.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?anyof.sender.contract=tz1...`, `?anyof.sender.initiator=tz1...`.
    /// </summary>
    public AnyOfParameter? Anyof { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the sender's contract code.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?senderCodeHash=123456`.
    /// </summary>
    public Int32NullParameter? SenderCodeHash { get; set; }

    /// <summary>
    /// Filters by initiator address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?initiator=tz1...`.
    /// </summary>
    public AddressInfoParameter? Initiator { get; set; }

    /// <summary>
    /// Filters by originated contract address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?contract=KT1...`.
    /// </summary>
    public AddressInfoParameter? Contract { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the originated contract's code.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?contractCodeHash=123456`.
    /// </summary>
    public Int32NullParameter? ContractCodeHash { get; set; }

    /// <summary>
    /// Filters by baker (delegate) address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?baker=tz1...`.
    /// </summary>
    public AddressInfoParameter? Baker { get; set; }

    [JsonIgnore]
    internal OrParameter? Or { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Anyof == null &&
        SenderCodeHash == null &&
        Initiator == null &&
        Contract == null &&
        ContractCodeHash == null &&
        Baker == null &&
        Or == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.anyof", Anyof),
        ($"{name}.senderCodeHash", SenderCodeHash),
        ($"{name}.initiator", Initiator),
        ($"{name}.contract", Contract),
        ($"{name}.contractCodeHash", ContractCodeHash),
        ($"{name}.baker", Baker),
        ($"{name}.or", Or));
}
