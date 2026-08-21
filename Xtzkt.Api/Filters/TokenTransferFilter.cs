using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class TokenTransferFilter : INormalizable
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
    /// Filters by token.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?token=123`, `?token.contract=KT1...&amp;token.tokenId=0`.
    /// </summary>
    public TokenInfoParameter? Token { get; set; }

    /// <summary>
    /// Matches an address against any of the listed fields (`from`, `to`), instead of just one.
    /// This is how you get everything related to an address in a single request, rather than
    /// querying each field separately and merging the results yourself.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?anyof.from.to=tz1...`, `?anyof.from.to.in=tz1...,0x...`.
    /// </summary>
    public AnyOfParameter? Anyof { get; set; }

    /// <summary>
    /// Filters by sender address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?from=tz1...`, `?from=null`.
    /// </summary>
    public AddressInfoNullParameter? From { get; set; }

    /// <summary>
    /// Filters by entrypoint via which the tokens were sent.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?fromEntrypoint=transfer`.
    /// </summary>
    public Utf8BytesParameter? FromEntrypoint { get; set; }

    /// <summary>
    /// Filters by target address.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?to=tz1...`, `?to=null`.
    /// </summary>
    public AddressInfoNullParameter? To { get; set; }

    /// <summary>
    /// Filters by entrypoint via which the tokens were received.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?toEntrypoint=transfer`.
    /// </summary>
    public Utf8BytesParameter? ToEntrypoint { get; set; }

    /// <summary>
    /// Filters by amount.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?amount.gt=0`.
    /// </summary>
    public BigIntegerParameter? Amount { get; set; }

    /// <summary>
    /// Filters by the transaction operation that caused the transfer.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?transactionId=123`, `?transactionId.ne=null`.
    /// </summary>
    public Int64NullParameter? TransactionId { get; set; }

    /// <summary>
    /// Filters by the origination operation that caused the transfer.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?originationId=123`.
    /// </summary>
    public Int64NullParameter? OriginationId { get; set; }

    /// <summary>
    /// Filters by the migration that caused the transfer.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?migrationId=123`.
    /// </summary>
    public Int64NullParameter? MigrationId { get; set; }

    [JsonIgnore]
    internal OrParameter? Or { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Level == null &&
        Timestamp == null &&
        Token == null &&
        Anyof == null &&
        From == null &&
        FromEntrypoint == null &&
        To == null &&
        ToEntrypoint == null &&
        Amount == null &&
        TransactionId == null &&
        OriginationId == null &&
        MigrationId == null &&
        Or == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.level", Level),
        ($"{name}.timestamp", Timestamp),
        ($"{name}.token", Token),
        ($"{name}.anyof", Anyof),
        ($"{name}.from", From),
        ($"{name}.fromEntrypoint", FromEntrypoint),
        ($"{name}.to", To),
        ($"{name}.toEntrypoint", ToEntrypoint),
        ($"{name}.amount", Amount),
        ($"{name}.transactionId", TransactionId),
        ($"{name}.originationId", OriginationId),
        ($"{name}.migrationId", MigrationId),
        ($"{name}.or", Or));
}
