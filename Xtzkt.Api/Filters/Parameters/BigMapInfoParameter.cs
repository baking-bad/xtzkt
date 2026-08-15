using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(BigMapInfoBinder))]
public class BigMapInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal unique bigmap id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? Id { get; set; }

    /// <summary>
    /// Filters by bigmap pointer, also known as bigmap id.
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? Ptr { get; set; }

    /// <summary>
    /// Filters by contract the bigmap belongs to.
    /// Click on the parameter to expand more details.
    /// </summary>
    public ContractInfoParameter? Contract { get; set; }

    /// <summary>
    /// Filters by path to the bigmap in the contract storage.
    /// Click on the parameter to expand more details.
    /// </summary>
    public StringParameter? StoragePath { get; set; }

    public virtual bool IsEmpty() =>
        Id == null &&
        Ptr == null &&
        Contract == null &&
        StoragePath == null;

    public virtual string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.ptr", Ptr),
        ($"{name}.contract", Contract),
        ($"{name}.storagePath", StoragePath));
}
