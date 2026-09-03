using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(BridgeTicketInfoBinder))]
public class BridgeTicketInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal bridge ticket id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int64Parameter? Id { get; set; }

    /// <summary>
    /// Filters by hash of the L1 ticket behind the bridged asset.
    /// Click on the parameter to expand more details.
    /// </summary>
    public HexBytesParameter? WeakHash { get; set; }

    public virtual bool IsEmpty() =>
        Id == null &&
        WeakHash == null;

    public virtual string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.weakHash", WeakHash));
}
