using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class BigMapUpdateFilter : INormalizable
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
    /// Filters by bigmap that was updated.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?bigMap=123`, `?bigMap.contract=KT1...`.
    /// </summary>
    public BigMapInfoParameter? BigMap { get; set; }

    /// <summary>
    /// Filters by bigmap key that was updated.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?bigMapKey=123`, `?bigMapKey.keyHash=expr...`.
    /// </summary>
    public BigMapKeyInfoParameter? BigMapKey { get; set; }

    /// <summary>
    /// Filters by action (`allocate`, `add_key`, `update_key`, `remove_key` or `remove`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?action=add_key`.
    /// </summary>
    public BigMapActionParameter? Action { get; set; }

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
    /// Filters by level of the block the item is in.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?level=1500000`, `?level.gt=1500000`.
    /// </summary>
    public Int32Parameter? Level { get; set; }

    /// <summary>
    /// Filters by timestamp of the block the item is in.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?timestamp.gt=2024-01-01T00:00:00Z`.
    /// </summary>
    public DateTimeParameter? Timestamp { get; set; }

    /// <summary>
    /// Filters by the transaction operation that caused the update.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?transactionId=123`, `?transactionId.null=false`.
    /// </summary>
    public Int64NullParameter? TransactionId { get; set; }

    /// <summary>
    /// Filters by the origination operation that caused the update.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?originationId=123`.
    /// </summary>
    public Int64NullParameter? OriginationId { get; set; }

    /// <summary>
    /// Filters by the migration that caused the update.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?migrationId=123`.
    /// </summary>
    public Int64NullParameter? MigrationId { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        BigMap == null &&
        BigMapKey == null &&
        Action == null &&
        RawValue == null &&
        Value == null &&
        Level == null &&
        Timestamp == null &&
        TransactionId == null &&
        OriginationId == null &&
        MigrationId == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.bigMap", BigMap),
        ($"{name}.bigMapKey", BigMapKey),
        ($"{name}.action", Action),
        ($"{name}.rawValue", RawValue),
        ($"{name}.value", Value),
        ($"{name}.level", Level),
        ($"{name}.timestamp", Timestamp),
        ($"{name}.transactionId", TransactionId),
        ($"{name}.originationId", OriginationId),
        ($"{name}.migrationId", MigrationId));
}
