using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;

namespace Xtzkt.Api.Filters;

public class LogFilter : INormalizable
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
    /// Filters by runtime (`evm` or `michelson`).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?runtime=evm`.
    /// </summary>
    public RuntimeParameter? Runtime { get; set; }

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
    /// Filters by address that emitted the log.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?address=tz1...`, `?address.in=tz1...,0x...`.
    /// </summary>
    public AddressInfoParameter? Address { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the parameter and storage types of the contract,
    /// whose code was executed when the log was emitted.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?contractTypeHash=123456`.
    /// </summary>
    public Int32Parameter? ContractTypeHash { get; set; }

    /// <summary>
    /// Filters by 32-bit hash of the code of the contract,
    /// whose code was executed when the log was emitted.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?contractCodeHash=123456`.
    /// </summary>
    public Int32Parameter? ContractCodeHash { get; set; }

    /// <summary>
    /// Filters by event name.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?name=Transfer`, `?name.ne=null`.
    /// </summary>
    public StringNullParameter? Name { get; set; }

    /// <summary>
    /// Filters by log payload in JSON format.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?payload.value.gt=0`.
    /// </summary>
    public JsonParameter? Payload { get; set; }

    /// <summary>
    /// Filters by which source the name and the payload were decoded with: `false` for logs,
    /// decoded with a trusted one (contract ABI for `evm`, event type for `michelson`),
    /// `true` for logs, where the only available source was a guess, made by matching the event
    /// signature hash against popular standards, because the contract ABI is unknown.
    ///
    /// Example: `?guessed=false`.
    /// </summary>
    public bool? Guessed { get; set; }

    /// <summary>
    /// Filters by the transaction operation that emitted the log.
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Examples: `?transactionId=123`, `?transactionId.ne=null`.
    /// </summary>
    public Int64NullParameter? TransactionId { get; set; }

    /// <summary>
    /// Filters by the origination operation that emitted the log (`evm` only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?originationId=123`.
    /// </summary>
    public Int64NullParameter? OriginationId { get; set; }

    /// <summary>
    /// Filters by the deposit operation that emitted the log (`evm` only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?depositId=123`.
    /// </summary>
    public Int64NullParameter? DepositId { get; set; }

    /// <summary>
    /// Filters by the first event topic, which is usually the event signature hash (`evm` only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?topic0=0xddf252ad...`.
    /// </summary>
    public HexBytesParameter? Topic0 { get; set; }

    /// <summary>
    /// Filters by the second event topic (`evm` only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?topic1=0x...`.
    /// </summary>
    public HexBytesParameter? Topic1 { get; set; }

    /// <summary>
    /// Filters by the third event topic (`evm` only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?topic2=0x...`.
    /// </summary>
    public HexBytesParameter? Topic2 { get; set; }

    /// <summary>
    /// Filters by the fourth event topic (`evm` only).
    ///
    /// Click on the parameter to expand more details.
    ///
    /// Example: `?topic3=0x...`.
    /// </summary>
    public HexBytesParameter? Topic3 { get; set; }

    public bool IsEmpty() =>
        Id == null &&
        Chain == null &&
        Runtime == null &&
        Level == null &&
        Timestamp == null &&
        Address == null &&
        ContractTypeHash == null &&
        ContractCodeHash == null &&
        Name == null &&
        Payload == null &&
        Guessed == null &&
        TransactionId == null &&
        OriginationId == null &&
        DepositId == null &&
        Topic0 == null &&
        Topic1 == null &&
        Topic2 == null &&
        Topic3 == null;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.id", Id),
        ($"{name}.chain", Chain),
        ($"{name}.runtime", Runtime),
        ($"{name}.level", Level),
        ($"{name}.timestamp", Timestamp),
        ($"{name}.address", Address),
        ($"{name}.contractTypeHash", ContractTypeHash),
        ($"{name}.contractCodeHash", ContractCodeHash),
        ($"{name}.name", Name),
        ($"{name}.payload", Payload),
        ($"{name}.guessed", Guessed),
        ($"{name}.transactionId", TransactionId),
        ($"{name}.originationId", OriginationId),
        ($"{name}.depositId", DepositId),
        ($"{name}.topic0", Topic0),
        ($"{name}.topic1", Topic1),
        ($"{name}.topic2", Topic2),
        ($"{name}.topic3", Topic3));
}
