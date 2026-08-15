using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class RegisterConstantOperationFilter : ManagerOperationFilter
{
    /// <summary>
    /// Filters by global address of the created constant (starts with `expr...`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?address=expr...`.
    /// </summary>
    public ExpressionParameter? Address { get; set; }

    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Address == null;

    public override string Normalize(string name) => base.Normalize(name) + ResponseCacheService.BuildKey("",
        ($"{name}.address", Address));
}
