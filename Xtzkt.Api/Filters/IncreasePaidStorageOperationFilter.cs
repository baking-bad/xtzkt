using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class IncreasePaidStorageOperationFilter : ManagerOperationFilter
{
    /// <summary>
    /// Filters by contract address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?contract=KT1...`.
    /// </summary>
    public AddressInfoParameter? Contract { get; set; }

    /// <summary>
    /// Filters by amount.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?amount.gt=0`.
    /// </summary>
    public BigIntegerParameter? Amount { get; set; }

    [JsonIgnore]
    internal OrParameter? Or { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Contract == null &&
        Amount == null &&
        Or == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.contract", Contract),
        ($"{name}.amount", Amount),
        ($"{name}.or", Or));
}
