using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class TokenFilter : INormalizable
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
    /// Filters by contract address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?contract=KT1...`.
    /// </summary>
    public AddressInfoParameter? Contract { get; set; }

    /// <summary>
    /// Filters by token id within the contract.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?tokenId=0`.
    /// </summary>
    public BigIntegerParameter? TokenId { get; set; }

    /// <summary>
    /// Filters by token standard (`fa1.2`, `fa2`, `erc20`, `erc721` or `erc1155`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?standard=fa2`, `?standard.ne=erc20`.
    /// </summary>
    public TokenStandardParameter? Standard { get; set; }

    /// <summary>
    /// Filters by level of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by level of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstTimestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? FirstTimestamp { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastTimestamp.lt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? LastTimestamp { get; set; }

    /// <summary>
    /// Filters by token metadata.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?metadata.symbol=USDC`.
    /// </summary>
    public JsonParameter? Metadata { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Contract == null &&
        TokenId == null &&
        Standard == null &&
        FirstLevel == null &&
        LastLevel == null &&
        FirstTimestamp == null &&
        LastTimestamp == null &&
        Metadata == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.contract", Contract),
        ($"{name}.tokenId", TokenId),
        ($"{name}.standard", Standard),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.lastLevel", LastLevel),
        ($"{name}.firstTimestamp", FirstTimestamp),
        ($"{name}.lastTimestamp", LastTimestamp),
        ($"{name}.metadata", Metadata));
}
