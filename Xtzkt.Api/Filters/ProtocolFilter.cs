using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class ProtocolFilter : INormalizable
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
    /// Filters by protocol hash (base58 for Tezos L1, hex kernel root hash for Tezos X).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?hash=P...`.
    /// </summary>
    public ProtocolHashParameter? Hash { get; set; }

    /// <summary>
    /// Filters by level of the first block under the protocol.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by level of the last block under the protocol.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Hash == null &&
        FirstLevel == null &&
        LastLevel == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.hash", Hash),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.lastLevel", LastLevel));
}
