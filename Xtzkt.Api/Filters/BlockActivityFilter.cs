using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class BlockActivityFilter : INormalizable
{
    /// <summary>
    /// Level of the block whose activity to return. Required. The same level exists on every chain,
    /// so add `chain` unless you want all of them at once.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?level=1500000`, `?level.in=1500000,1500001`.
    /// </summary>
    public required Int32EqParameter Level { get; set; }

    /// <summary>
    /// Filters by chain the item belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainInfoParameter? Chain { get; set; }

    /// <summary>
    /// Comma-separated list of activity types to return. If not specified, most types are returned,
    /// except the noisy ones such as attestations, which you have to ask for explicitly.
    ///
    /// Examples: `?types=transaction`, `?types=transaction,token_transfer,origination`.
    /// </summary>
    public ActivityTypesParameter? Types { get; set; }

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.level", Level),
        ($"{name}.chain", Chain),
        ($"{name}.types", Types));
}
