using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class AccountActivityFilter : INormalizable
{
    /// <summary>
    /// Address whose activity to return. Required. Accepts one address, or several to get a merged feed.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?address=tz1...`, `?address.in=tz1...,0x...`.
    /// </summary>
    public required AddressHashEqParameter Address { get; set; }

    /// <summary>
    /// Comma-separated list of activity types to return. If not specified, most types are returned,
    /// except the noisy ones such as attestations, which you have to ask for explicitly.
    ///
    /// Examples: `?types=transaction`, `?types=transaction,token_transfer,origination`.
    /// </summary>
    public ActivityTypesParameter? Types { get; set; }

    /// <summary>
    /// Comma-separated list of roles the address must have played (`sender`, `target`, `initiator`,
    /// `mention`, or `all`). If not specified, any role matches.
    ///
    /// Examples: `?roles=sender`, `?roles=sender,target`.
    /// </summary>
    public ActivityRolesParameter? Roles { get; set; }

    /// <summary>
    /// Filters by chain the item belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?chain=0`, `?chain.chainId=NetXdQprcVkpaWU`.
    /// </summary>
    public ChainInfoParameter? Chain { get; set; }

    /// <summary>
    /// Filters by timestamp of the block the item is in.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?timestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? Timestamp { get; set; }

    public bool IsEmpty() =>
        Address == null &&
        Types == null &&
        Roles == null &&
        Chain == null &&
        Timestamp == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.address", Address),
        ($"{name}.types", Types),
        ($"{name}.roles", Roles),
        ($"{name}.chain", Chain),
        ($"{name}.timestamp", Timestamp));
}
