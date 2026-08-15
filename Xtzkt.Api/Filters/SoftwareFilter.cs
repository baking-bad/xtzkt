using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class SoftwareFilter : INormalizable
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
    /// Filters by chain the item belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainInfoParameter? Chain { get; set; }

    /// <summary>
    /// Filters by short hash.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?shortHash=1a2b3c4d`.
    /// </summary>
    public HashParameter? ShortHash { get; set; }

    /// <summary>
    /// Filters by level of the first block produced by the software.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by level of the last block produced by the software.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        ShortHash == null &&
        FirstLevel == null &&
        LastLevel == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.shortHash", ShortHash),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.lastLevel", LastLevel));
}
