using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class AddressFilter : INormalizable
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
    /// Filters by address hash.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?hash=tz1...`, `?hash.in=tz1...,0x...`.
    /// </summary>
    public AddressHashParameter? Hash { get; set; }

    /// <summary>
    /// Filters by address type.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?type=l1_user`, `?type.in=l1_contract,x_evm_contract`.
    /// </summary>
    public AddressTypeParameter? Type { get; set; }

    /// <summary>
    /// Filters by layer.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?layer=l1`.
    /// </summary>
    public LayerParameter? Layer { get; set; }

    /// <summary>
    /// Filters by runtime.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?runtime=evm`.
    /// </summary>
    public RuntimeParameter? Runtime { get; set; }

    /// <summary>
    /// Filters by level of the block where the address first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the address first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstTimestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? FirstTimestamp { get; set; }

    /// <summary>
    /// Filters by level of the block where the address was last seen.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the address was last seen.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastTimestamp.lt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? LastTimestamp { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Hash == null &&
        Type == null &&
        Layer == null &&
        Runtime == null &&
        FirstLevel == null &&
        FirstTimestamp == null &&
        LastLevel == null &&
        LastTimestamp == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.hash", Hash),
        ($"{name}.type", Type),
        ($"{name}.layer", Layer),
        ($"{name}.runtime", Runtime),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.firstTimestamp", FirstTimestamp),
        ($"{name}.lastLevel", LastLevel),
        ($"{name}.lastTimestamp", LastTimestamp));
}
