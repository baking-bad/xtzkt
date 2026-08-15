using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class Eip7702DelegationFilter : INormalizable
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
    /// Filters by the transaction operation that carried the authorization.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?transactionId=123`.
    /// </summary>
    public Int64Parameter? TransactionId { get; set; }

    /// <summary>
    /// Filters by the address that sent the transaction carrying the authorization.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?sender=tz1...`, `?sender.in=tz1...,0x...`.
    /// </summary>
    public AddressInfoParameter? Sender { get; set; }

    /// <summary>
    /// Filters by account that signed the authorization.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?authority=0x...`.
    /// </summary>
    public AddressInfoParameter? Authority { get; set; }

    /// <summary>
    /// Filters by authority nonce the authorization was signed with.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?nonce=0`.
    /// </summary>
    public Int32Parameter? Nonce { get; set; }

    /// <summary>
    /// Filters by contract the authority had been delegated to before.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?prevDelegate.null=true`.
    /// </summary>
    public AddressInfoNullParameter? PrevDelegate { get; set; }

    /// <summary>
    /// Filters by contract the authority is delegated to.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?delegate=0x...`, `?delegate.null=true`.
    /// </summary>
    public AddressInfoNullParameter? Delegate { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Level == null &&
        Timestamp == null &&
        TransactionId == null &&
        Sender == null &&
        Authority == null &&
        Nonce == null &&
        PrevDelegate == null &&
        Delegate == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.level", Level),
        ($"{name}.timestamp", Timestamp),
        ($"{name}.transactionId", TransactionId),
        ($"{name}.sender", Sender),
        ($"{name}.authority", Authority),
        ($"{name}.nonce", Nonce),
        ($"{name}.prevDelegate", PrevDelegate),
        ($"{name}.delegate", Delegate));
}
