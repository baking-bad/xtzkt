using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class BlockFilter : INormalizable
{
    /// <summary>
    /// Filters by internal unique id. Within a chain ids grow over time, so sorting by id sorts chronologically.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?id=123`, `?id.in=123,456`.
    /// </summary>
    public Int64Parameter? Id { get; set; }

    /// <summary>
    /// Filters by chain the item belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainInfoParameter? Chain { get; set; }

    /// <summary>
    /// Filters by block level.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?level=1500000`, `?level.gt=1500000`.
    /// </summary>
    public Int32Parameter? Level { get; set; }

    /// <summary>
    /// Filters by block timestamp.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?timestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? Timestamp { get; set; }

    /// <summary>
    /// Filters by block hash (base58 for Tezos L1, hex for Tezos X).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?hash=B...`.
    /// </summary>
    public BlockHashParameter? Hash { get; set; }

    /// <summary>
    /// Filters by Michelson block hash (Tezos X only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?michelsonHash=B...`.
    /// </summary>
    public MichelsonBlockHashParameter? MichelsonHash { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Level == null &&
        Timestamp == null &&
        Hash == null &&
        MichelsonHash == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.level", Level),
        ($"{name}.timestamp", Timestamp),
        ($"{name}.hash", Hash),
        ($"{name}.michelsonHash", MichelsonHash));
}
