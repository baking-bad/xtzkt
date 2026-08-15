using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(ChainInfoBinder))]
public class ChainInfoParameter : INormalizable
{
    /// <summary>
    /// Filters by internal unique chain id.
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32Parameter? Id { get; set; }

    /// <summary>
    /// Filters by publicly known chain id (base58 string for Tezos L1, hex string for Tezos X).
    /// Click on the parameter to expand more details.
    /// </summary>
    public ChainIdParameter? ChainId { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        ChainId == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chainId", ChainId));
}
