using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class BlockActivityFilter : INormalizable
{
    /// <summary>
    /// Chain the block belongs to, by either of its two ids. **Required**, and exactly one — a level
    /// only identifies a block within one chain.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public required ChainInfoEqParameter Chain { get; set; }

    /// <summary>
    /// Level of the block whose activity to return. **Required**, and exactly one — for several blocks,
    /// ask for them separately.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?level=1500000`.
    /// </summary>
    public required Int32EqParameter Level { get; set; }

    /// <summary>
    /// Comma-separated list of activity types to return. If not specified, most types are returned,
    /// except the noisy ones such as attestations, which you have to ask for explicitly.
    ///
    /// Examples: `?types=transaction`, `?types=transaction,token_transfer,origination`.
    /// </summary>
    public ActivityTypesParameter? Types { get; set; }

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.chain", Chain),
        ($"{name}.level", Level),
        ($"{name}.types", Types));
}
