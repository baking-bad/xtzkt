using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class StorageFilter : INormalizable
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
    /// Filters by contract the storage belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?contract=KT1...`, `?contract.codeHash=123456`.
    /// </summary>
    public ContractInfoParameter? Contract { get; set; }

    /// <summary>
    /// Filters by level of the block the item is in.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?level=1500000`, `?level.gt=1500000`.
    /// </summary>
    public Int32Parameter? Level { get; set; }

    /// <summary>
    /// Filters by status: `true` for the current storage of the contract, `false` for the historical ones.
    ///
    /// Example: `?current=true`.
    /// </summary>
    public bool? Current { get; set; }

    /// <summary>
    /// Filters by storage value in Micheline format (specified as a JSON value).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?rawValue={"int":"0"}`.
    /// </summary>
    public MichelineParameter? RawValue { get; set; }

    /// <summary>
    /// Filters by storage value in JSON format.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?value.balance.gt=0`.
    /// </summary>
    public JsonParameter? Value { get; set; }

    /// <summary>
    /// Filters by the transaction operation that set the storage.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?transactionId=123`, `?transactionId.null=false`.
    /// </summary>
    public Int64NullParameter? TransactionId { get; set; }

    /// <summary>
    /// Filters by the origination operation that set the storage.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?originationId=123`.
    /// </summary>
    public Int64NullParameter? OriginationId { get; set; }

    /// <summary>
    /// Filters by the migration that set the storage.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?migrationId=123`.
    /// </summary>
    public Int64NullParameter? MigrationId { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Contract == null &&
        Level == null &&
        Current == null &&
        RawValue == null &&
        Value == null &&
        TransactionId == null &&
        OriginationId == null &&
        MigrationId == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.contract", Contract),
        ($"{name}.level", Level),
        ($"{name}.current", Current),
        ($"{name}.rawValue", RawValue),
        ($"{name}.value", Value),
        ($"{name}.transactionId", TransactionId),
        ($"{name}.originationId", OriginationId),
        ($"{name}.migrationId", MigrationId));
}
