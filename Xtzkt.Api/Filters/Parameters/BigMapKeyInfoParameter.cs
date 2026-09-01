using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(BigMapKeyInfoBinder))]
public class BigMapKeyInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal unique bigmap key id (default).
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int64NullParameter? Id { get; set; }

    /// <summary>
    /// Filters by key hash (script expression).
    /// Click on the parameter to expand more details.
    /// </summary>
    public ExpressionBytesParameter? KeyHash { get; set; }

    /// <summary>
    /// Filters by key in Micheline format (specified as a JSON value).
    /// Click on the parameter to expand more details.
    /// </summary>
    public MichelineParameter? RawKey { get; set; }

    /// <summary>
    /// Filters by key in JSON format.
    /// Click on the parameter to expand more details.
    /// </summary>
    public JsonParameter? Key { get; set; }

    public virtual bool IsEmpty() =>
        Id == null &&
        KeyHash == null &&
        RawKey == null &&
        Key == null;

    public virtual string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.keyHash", KeyHash),
        ($"{name}.rawKey", RawKey),
        ($"{name}.key", Key));
}
