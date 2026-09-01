using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class BigMapKeyFilter : INormalizable
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
    /// Filters by bigmap the key belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?bigMap=123`, `?bigMap.contract=KT1...`.
    /// </summary>
    public BigMapInfoParameter? BigMap { get; set; }

    /// <summary>
    /// Filters by status: `true` for active keys, `false` for removed ones.
    ///
    /// Example: `?active=true`.
    /// </summary>
    public bool? Active { get; set; }

    /// <summary>
    /// Filters by key hash (script expression).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?keyHash=expr...`.
    /// </summary>
    public ExpressionBytesParameter? KeyHash { get; set; }

    /// <summary>
    /// Filters by key in Micheline format (specified as a JSON value).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?rawKey={"string":"tz1..."}`.
    /// </summary>
    public MichelineParameter? RawKey { get; set; }

    /// <summary>
    /// Filters by key in JSON format.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?key=tz1...`, `?key.owner=tz1...`.
    /// </summary>
    public JsonParameter? Key { get; set; }

    /// <summary>
    /// Filters by value in Micheline format (specified as a JSON value).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?rawValue={"int":"0"}`.
    /// </summary>
    public MichelineParameter? RawValue { get; set; }

    /// <summary>
    /// Filters by value in JSON format.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?value.balance.gt=0`.
    /// </summary>
    public JsonParameter? Value { get; set; }

    /// <summary>
    /// Filters by level of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstLevel.gt=1500000`.
    /// </summary>
    public Int32Parameter? FirstLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item first appeared.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?firstTimestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? FirstTimestamp { get; set; }

    /// <summary>
    /// Filters by level of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastLevel.lt=1500000`.
    /// </summary>
    public Int32Parameter? LastLevel { get; set; }

    /// <summary>
    /// Filters by timestamp of the block where the item was last active.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?lastTimestamp.lt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? LastTimestamp { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        BigMap == null &&
        Active == null &&
        KeyHash == null &&
        RawKey == null &&
        Key == null &&
        RawValue == null &&
        Value == null &&
        FirstLevel == null &&
        FirstTimestamp == null &&
        LastLevel == null &&
        LastTimestamp == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.bigMap", BigMap),
        ($"{name}.active", Active),
        ($"{name}.keyHash", KeyHash),
        ($"{name}.rawKey", RawKey),
        ($"{name}.key", Key),
        ($"{name}.rawValue", RawValue),
        ($"{name}.value", Value),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.firstTimestamp", FirstTimestamp),
        ($"{name}.lastLevel", LastLevel),
        ($"{name}.lastTimestamp", LastTimestamp));
}
