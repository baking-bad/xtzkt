using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class OpgActivityFilter : INormalizable
{
    /// <summary>
    /// Hash of the operation group whose activity to return. Required.
    /// Base58 (`o...`) for Michelson, hex (`0x...`) for EVM.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?hash=o...`, `?hash=0x...`.
    /// </summary>
    public required OperationHashEqParameter Hash { get; set; }

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
    /// Examples: `?types=transaction`, `?types=transaction,token_transfer`.
    /// </summary>
    public ActivityTypesParameter? Types { get; set; }

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.hash", Hash),
        ($"{name}.chain", Chain),
        ($"{name}.types", Types));
}
