using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters.Parameters;

/// <summary>
/// Selects exactly one chain, by either of its two ids. Unlike <see cref="ChainInfoParameter"/> it has no
/// list or negation modes, so it always resolves to a single chain (or to none) - which is what endpoints
/// keyed on something chain-local, such as a block level, need.
/// </summary>
[ModelBinder(BinderType = typeof(ChainInfoEqBinder))]
public class ChainInfoEqParameter : INormalizable
{
    /// <summary>
    /// Filters by internal unique chain id.
    /// Click on the parameter to expand more details.
    /// </summary>
    public Int32EqParameter? Id { get; set; }

    /// <summary>
    /// Filters by publicly known chain id (base58 string for Tezos L1, hex string for Tezos X).
    /// Click on the parameter to expand more details.
    /// </summary>
    public ChainIdEqParameter? ChainId { get; set; }

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chainId", ChainId));
}
