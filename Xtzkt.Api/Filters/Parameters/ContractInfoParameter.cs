using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(ContractInfoBinder))]
public class ContractInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal unique contract id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? Id { get; set; }

    /// <summary>
    /// Filters by contract address hash.
    /// Click on the parameter to expand more details.
    /// </summary>
    public AddressHashParameter? Hash { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the contract parameter and storage types.
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? TypeHash { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the contract code.
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? CodeHash { get; set; }

    /// <summary>
    /// Filters by address that created the contract.
    /// Click on the parameter to expand more details.
    /// </summary>
    public AddressInfoParameter? Creator { get; set; }

    public virtual bool IsEmpty() =>
        Id == null &&
        Hash == null &&
        TypeHash == null &&
        CodeHash == null &&
        Creator == null;

    public virtual string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.hash", Hash),
        ($"{name}.typeHash", TypeHash),
        ($"{name}.codeHash", CodeHash),
        ($"{name}.creator", Creator));
}
