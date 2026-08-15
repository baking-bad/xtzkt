using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class ChainFilter : INormalizable
{
    /// <summary>
    /// Filters by internal unique id.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?id=123`, `?id.in=1,2,3`.
    /// </summary>
    public Int32Parameter? Id { get; set; }

    /// <summary>
    /// Filters by publicly known chain id (base58 string for Tezos L1, hex string for Tezos X).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainIdParameter? ChainId { get; set; }

    /// <summary>
    /// Filters by layer (`l1` or `x`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?layer=l1`.
    /// </summary>
    public LayerParameter? Layer { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        ChainId == null &&
        Layer == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chainId", ChainId),
        ($"{name}.layer", Layer));
}
