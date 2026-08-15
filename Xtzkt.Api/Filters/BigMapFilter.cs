using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class BigMapFilter : INormalizable
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
    /// Filters by bigmap pointer, also known as bigmap id.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?ptr=123`.
    /// </summary>
    public Int32Parameter? Ptr { get; set; }

    /// <summary>
    /// Filters by contract the bigmap belongs to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?contract=KT1...`, `?contract.codeHash=123456`.
    /// </summary>
    public ContractInfoParameter? Contract { get; set; }

    /// <summary>
    /// Filters by path to the bigmap in the contract storage.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?storagePath=ledger`.
    /// </summary>
    public StringParameter? StoragePath { get; set; }

    /// <summary>
    /// Filters by status: `true` for allocated bigmaps, `false` for removed ones.
    ///
    /// Example: `?active=true`.
    /// </summary>
    public bool? Active { get; set; }

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

    /// <summary>
    /// Filters by tags (`persistent`, `metadata`, `token_metadata`, `ledger`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?tags=ledger`, `?tags.any=ledger,token_metadata`.
    /// </summary>
    public BigMapTagsParameter? Tags { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Ptr == null &&
        Contract == null &&
        StoragePath == null &&
        Active == null &&
        FirstLevel == null &&
        FirstTimestamp == null &&
        LastLevel == null &&
        LastTimestamp == null &&
        Tags == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.ptr", Ptr),
        ($"{name}.contract", Contract),
        ($"{name}.storagePath", StoragePath),
        ($"{name}.active", Active),
        ($"{name}.firstLevel", FirstLevel),
        ($"{name}.firstTimestamp", FirstTimestamp),
        ($"{name}.lastLevel", LastLevel),
        ($"{name}.lastTimestamp", LastTimestamp),
        ($"{name}.tags", Tags));
}
