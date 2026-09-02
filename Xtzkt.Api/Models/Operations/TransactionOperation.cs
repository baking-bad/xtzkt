using System.Numerics;
using System.Text.Json.Serialization;
using Netezos.Encoding;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "direction")]
[JsonDerivedType(typeof(L1TransactionOperation), Directions.L1)]
[JsonDerivedType(typeof(XEvmTransactionOperation), Directions.XEvm)]
[JsonDerivedType(typeof(XMichelsonTransactionOperation), Directions.XMichelson)]
[JsonDerivedType(typeof(XEvmMichelsonTransactionOperation), Directions.XEvmMichelson)]
[JsonDerivedType(typeof(XMichelsonEvmTransactionOperation), Directions.XMichelsonEvm)]
public abstract class TransactionOperation : IOpgActivity, ITokenTransfersSource
{
    /// <summary>Internal unique operation id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the operation belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Level of the block the operation was included in.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block the operation was included in.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Hash of the operation group.</summary>
    public required string Hash { get; set; }

    /// <summary>Address that sent the transaction.</summary>
    public required AddressInfo Sender { get; set; }

    /// <summary>Code hash of the sender, if the transaction was sent by a contract.</summary>
    public int? SenderCodeHash { get; set; }

    /// <summary>Address that started the whole operation chain, if this is an internal operation.</summary>
    public AddressInfo? Initiator { get; set; }

    /// <summary>Address the transaction was sent to.</summary>
    public required AddressInfo Target { get; set; }

    /// <summary>Code hash of the target, if it's a contract.</summary>
    public int? TargetCodeHash { get; set; }

    /// <summary>Sender's operation counter (nonce in EVM), ensuring operations apply in order and only once.</summary>
    public int Counter { get; set; }

    /// <summary>Maximum gas the sender allowed the operation to consume, if it is an external one.</summary>
    public int? GasLimit { get; set; }

    /// <summary>Gas the operation actually consumed.</summary>
    public int GasUsed { get; set; }

    /// <summary>Operation status (`applied`, `failed`, `backtracked`, `skipped`).</summary>
    public required string Status { get; set; }

    /// <summary>Errors the operation failed with, if it wasn't applied.</summary>
    public string? Errors { get; set; }

    /// <summary>Called entrypoint (function name in EVM), if the target is a contract.</summary>
    public string? Entrypoint { get; set; }

    /// <summary>Call arguments in JSON format, if the target is a contract.</summary>
    public RawJson? Parameters { get; set; }

    /// <summary>
    /// Which source the entrypoint, the parameters and the result were decoded with:
    /// `false` for a trusted one (contract ABI for `evm`, contract schema for `michelson`),
    /// `true` if the only available source was a guess, made by matching the function selector
    /// against popular standards, because the contract ABI is unknown,
    /// `null` if there was no source to decode them with at all.
    /// Note, these fields may be `null` even when the source is known, if they failed to decode.
    /// </summary>
    public bool? Guessed { get; set; }

    /// <summary>Number of token transfers caused by the operation, if any.</summary>
    public int? TokenTransfers { get; set; }

    /// <summary>Number of internal operations caused by the operation, if any.</summary>
    public int? InternalOperations { get; set; }

    /// <summary>Number of logs (events) emitted by the operation, if any.</summary>
    public int? LogsCount { get; set; }
}

public abstract class MichelsonTransactionOperation : TransactionOperation, ITicketTransfersSource
{
    /// <summary>Amount transferred to the target (mutez).</summary>
    public long Amount { get; set; }

    /// <summary>Amount burned for the storage the operation used (mutez).</summary>
    public long? StorageFee { get; set; }

    /// <summary>Amount burned for allocating a new address (mutez).</summary>
    public long? AllocationFee { get; set; }

    /// <summary>Maximum storage the sender allowed the operation to allocate (bytes), if it is an external one.</summary>
    public int? StorageLimit { get; set; }

    /// <summary>Storage the operation actually allocated (bytes).</summary>
    public int StorageUsed { get; set; }

    /// <summary>Position of the operation among the internal operations of its parent, if it's an internal one.</summary>
    public int? Nonce { get; set; }

    /// <summary>Number of bigmap updates caused by the operation, if any.</summary>
    public int? BigMapUpdates { get; set; }

    /// <summary>Number of ticket transfers caused by the operation, if any.</summary>
    public int? TicketTransfers { get; set; }

    /// <summary>Call arguments in Micheline format, if the target is a contract.</summary>
    public IMicheline? ParametersRaw { get; set; }
}

public class L1TransactionOperation : MichelsonTransactionOperation
{
    /// <summary>Fee paid to the baker for including the operation (mutez), if it is an external one.</summary>
    public long? BakerFee { get; set; }
}

public class XMichelsonTransactionOperation : MichelsonTransactionOperation
{
    /// <summary>Fee paid for posting the operation data to L1, based on its size (mutez), if it is an external one.</summary>
    public long? DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation was allowed to consume (mutez), if it is an external one.</summary>
    public long? GasFee { get; set; }

    /// <summary>Part of the gas fee returned to the sender for the gas that wasn't consumed (mutez), if it is an external one.</summary>
    public long? GasRefund { get; set; }
}

public class XEvmTransactionOperation : TransactionOperation, IBridgeTicketTransfersSource
{
    /// <summary>
    /// Transaction type it was sent as (`legacy`, `dynamic_fee`, `set_code`, ...),
    /// or `trace` if it's an internal operation rather than a transaction of its own.
    /// </summary>
    public required string OpType { get; set; }

    /// <summary>EVM opcode that made the call (`call`, `delegate_call`, `static_call`, ...).</summary>
    public required string OpCode { get; set; }

    /// <summary>Amount transferred to the target (18 decimals).</summary>
    public BigInteger Amount { get; set; }

    /// <summary>Fee paid for posting the operation data to L1, based on its size (18 decimals), if it is an external one.</summary>
    public BigInteger? DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation consumed (18 decimals), if it is an external one.</summary>
    public BigInteger? GasFee { get; set; }

    /// <summary>Gas price the sender offered, for `legacy` and `access_list` transactions (18 decimals).</summary>
    public BigInteger? GasPrice { get; set; }

    /// <summary>Maximum total price per gas the sender agreed to pay (18 decimals).</summary>
    public BigInteger? MaxFeePerGas { get; set; }

    /// <summary>Maximum tip per gas the sender agreed to pay on top of the base fee (18 decimals).</summary>
    public BigInteger? MaxPriorityFeePerGas { get; set; }

    /// <summary>Price per gas the sender was actually charged (18 decimals).</summary>
    public BigInteger? EffectiveGasPrice { get; set; }

    /// <summary>Raw call data sent to the target.</summary>
    [JsonConverter(typeof(HexConverter))]
    public byte[]? Input { get; set; }

    /// <summary>Raw data returned by the target.</summary>
    [JsonConverter(typeof(HexConverter))]
    public byte[]? Output { get; set; }

    /// <summary>Returned data decoded to JSON, if the target ABI is known.</summary>
    public RawJson? Result { get; set; }

    /// <summary>Number of EIP-7702 delegations set by the operation, if any.</summary>
    public int? Eip7702DelegationCount { get; set; }

    /// <summary>Number of bridge ticket transfers caused by the operation, if any.</summary>
    public int? BridgeTicketTransfers { get; set; }

    /// <summary>
    /// Id of the deposit operation the operation claimed off the queue, if it was a call to a bridge's
    /// claim entrypoint. Mind that this is the deposit's `id`, not its `depositId` (the queue nonce).
    /// </summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? ClaimDepositId { get; set; }
}

public class XEvmMichelsonTransactionOperation : TransactionOperation, ITicketTransfersSource
{
    /// <summary>
    /// Transaction type it was sent as (`legacy`, `dynamic_fee`, `set_code`, ...),
    /// or `trace` if it's an internal operation rather than a transaction of its own.
    /// </summary>
    public required string OpType { get; set; }

    /// <summary>EVM opcode that made the call (`call`, `delegate_call`, `static_call`, ...).</summary>
    public required string OpCode { get; set; }

    /// <summary>Amount the EVM sender sent (18 decimals).</summary>
    public BigInteger AmountSent { get; set; }

    /// <summary>
    /// Amount lost when converting the sent value to 6 decimals, since the remainder below
    /// one mutez can't be transferred (18 decimals).
    /// </summary>
    public BigInteger RoundingLoss { get; set; }

    /// <summary>Amount the Michelson target received (mutez).</summary>
    public long AmountReceived { get; set; }

    /// <summary>Fee paid for posting the operation data to L1, based on its size (18 decimals), if it is an external one.</summary>
    public BigInteger? DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation consumed (18 decimals), if it is an external one.</summary>
    public BigInteger? GasFee { get; set; }

    /// <summary>Gas price the sender offered, for `legacy` and `access_list` transactions (18 decimals).</summary>
    public BigInteger? GasPrice { get; set; }

    /// <summary>Maximum total price per gas the sender agreed to pay (18 decimals).</summary>
    public BigInteger? MaxFeePerGas { get; set; }

    /// <summary>Maximum tip per gas the sender agreed to pay on top of the base fee (18 decimals).</summary>
    public BigInteger? MaxPriorityFeePerGas { get; set; }

    /// <summary>Price per gas the sender was actually charged (18 decimals).</summary>
    public BigInteger? EffectiveGasPrice { get; set; }

    /// <summary>Number of bigmap updates caused by the operation, if any.</summary>
    public int? BigMapUpdates { get; set; }

    /// <summary>Number of ticket transfers caused by the operation, if any.</summary>
    public int? TicketTransfers { get; set; }

    /// <summary>Call arguments in Micheline format, if the target is a contract.</summary>
    public IMicheline? ParametersRaw { get; set; }

    /// <summary>Michelson alias (`KT1...`) of the sender — who the target sees as the caller.</summary>
    public required AddressInfo Alias { get; set; }

    /// <summary>EVM precompile the sender called to reach the Michelson runtime.</summary>
    public required AddressInfo Gateway { get; set; }

    /// <summary>Gateway function the sender called.</summary>
    public string? GatewayEntrypoint { get; set; }

    /// <summary>Arguments the gateway was called with, in JSON format.</summary>
    public RawJson? GatewayParameters { get; set; }

    /// <summary>Raw call data sent to the gateway.</summary>
    [JsonConverter(typeof(HexConverter))]
    public byte[]? GatewayInput { get; set; }

    /// <summary>Number of EIP-7702 delegations set by the operation, if any.</summary>
    public int? Eip7702DelegationCount { get; set; }
}

public class XMichelsonEvmTransactionOperation : TransactionOperation, IBridgeTicketTransfersSource
{
    /// <summary>Amount the Michelson sender sent (mutez).</summary>
    public long AmountSent { get; set; }

    /// <summary>Amount the EVM target received (18 decimals).</summary>
    public BigInteger AmountReceived { get; set; }

    /// <summary>Fee paid for posting the operation data to L1, based on its size (mutez), if it is an external one.</summary>
    public long? DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation was allowed to consume (mutez), if it is an external one.</summary>
    public long? GasFee { get; set; }

    /// <summary>Part of the gas fee returned to the sender for the gas that wasn't consumed (mutez), if it is an external one.</summary>
    public long? GasRefund { get; set; }

    /// <summary>Amount burned for the storage the operation used (mutez).</summary>
    public long? StorageFee { get; set; }

    /// <summary>Amount burned for allocating a new address (mutez).</summary>
    public long? AllocationFee { get; set; }

    /// <summary>Maximum storage the sender allowed the operation to allocate (bytes), if it is an external one.</summary>
    public int? StorageLimit { get; set; }

    /// <summary>Storage the operation actually allocated (bytes).</summary>
    public int StorageUsed { get; set; }

    /// <summary>Position of the operation among the internal operations of its parent, if it's an internal one.</summary>
    public int? Nonce { get; set; }

    /// <summary>Raw call data sent to the target.</summary>
    [JsonConverter(typeof(HexConverter))]
    public byte[]? Input { get; set; }

    /// <summary>Raw data returned by the target.</summary>
    [JsonConverter(typeof(HexConverter))]
    public byte[]? Output { get; set; }

    /// <summary>Returned data decoded to JSON, if the target ABI is known.</summary>
    public RawJson? Result { get; set; }

    /// <summary>EVM alias (`0x...`) of the sender — who the target sees as the caller.</summary>
    public required AddressInfo Alias { get; set; }

    /// <summary>Michelson contract the sender called to reach the EVM runtime.</summary>
    public required AddressInfo Gateway { get; set; }

    /// <summary>Gateway entrypoint the sender called.</summary>
    public string? GatewayEntrypoint { get; set; }

    /// <summary>Arguments the gateway was called with, in JSON format.</summary>
    public RawJson? GatewayParameters { get; set; }

    /// <summary>Arguments the gateway was called with, in Micheline format.</summary>
    public IMicheline? GatewayParametersRaw { get; set; }

    /// <summary>Number of bridge ticket transfers caused by the operation, if any.</summary>
    public int? BridgeTicketTransfers { get; set; }

    /// <summary>
    /// Id of the deposit operation the operation claimed off the queue, if it was a call to a bridge's
    /// claim entrypoint. Mind that this is the deposit's `id`, not its `depositId` (the queue nonce).
    /// </summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? ClaimDepositId { get; set; }
}
