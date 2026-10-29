using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(AssetInfoBinder))]
public class AssetInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal unique asset id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32NullParameter? Id { get; set; }

    public bool IsEmpty() =>
        Id == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id));
}
