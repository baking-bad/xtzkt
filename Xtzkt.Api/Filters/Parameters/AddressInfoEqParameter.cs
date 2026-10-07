using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(AddressInfoEqBinder))]
public class AddressInfoEqParameter : INormalizable
{
    /// <summary>
    /// Filters by internal unique address id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32EqInParameter? Id { get; set; }

    /// <summary>
    /// Filters by address hash.
    /// Click on the parameter to expand more details.
    /// </summary>
    public AddressHashEqParameter? Hash { get; set; }

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.hash", Hash));
}
