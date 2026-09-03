using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "env")]
[JsonDerivedType(typeof(L1OriginationOperation), Envs.L1)]
[JsonDerivedType(typeof(XEvmOriginationOperation), Envs.XEvm)]
[JsonDerivedType(typeof(XMichelsonOriginationOperation), Envs.XMichelson)]
public abstract class OriginationOperation : IOpgActivity, ITokenTransfersSource
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

    /// <summary>Address that deployed the contract.</summary>
    public required AddressInfo Sender { get; set; }

    /// <summary>Code hash of the sender, if the contract was deployed by another contract.</summary>
    public int? SenderCodeHash { get; set; }

    /// <summary>Address that started the whole operation chain, if this is an internal operation.</summary>
    public AddressInfo? Initiator { get; set; }

    /// <summary>Sender's operation counter, ensuring operations apply in order and only once.</summary>
    public int Counter { get; set; }

    /// <summary>Maximum gas the sender allowed the operation to consume, if it is an external one.</summary>
    public int? GasLimit { get; set; }

    /// <summary>
    /// Gas charged as used by the operation, excluding the gas reserved to cover the `daFee`. For EVM
    /// operations this is before the end-of-transaction gas refund, and it can exceed what the operation
    /// actually spent: an exceptional halt is charged its whole allowance, and a transaction below the
    /// EIP-7623 calldata floor is charged that floor.
    /// </summary>
    public int GasUsed { get; set; }

    /// <summary>Operation status (`applied`, `failed`, `backtracked`, `skipped`).</summary>
    public required string Status { get; set; }

    /// <summary>Errors the operation failed with, if it wasn't applied.</summary>
    public string? Errors { get; set; }

    /// <summary>Contract that was deployed. `null` if the operation wasn't applied.</summary>
    public AddressInfo? Contract { get; set; }

    /// <summary>32-bit hash of the deployed contract's code (helps to find identical contracts).</summary>
    public int? ContractCodeHash { get; set; }

    /// <summary>Number of token transfers caused by the operation, if any.</summary>
    public int? TokenTransfers { get; set; }
}

public abstract class MichelsonOriginationOperation : OriginationOperation
{
    /// <summary>Amount burned for the storage the operation used (mutez).</summary>
    public long? StorageFee { get; set; }

    /// <summary>Amount burned for allocating the new contract address (mutez).</summary>
    public long? AllocationFee { get; set; }

    /// <summary>Maximum storage the sender allowed the operation to allocate (bytes), if it is an external one.</summary>
    public int? StorageLimit { get; set; }

    /// <summary>Storage the operation actually allocated (bytes).</summary>
    public int StorageUsed { get; set; }

    /// <summary>Position of the operation among the internal operations of its parent, if it's an internal one.</summary>
    public int? Nonce { get; set; }

    /// <summary>Number of bigmap updates caused by the operation, if any.</summary>
    public int? BigMapUpdates { get; set; }

    /// <summary>Amount transferred to the new contract on deployment (mutez).</summary>
    public long Balance { get; set; }
}

public class L1OriginationOperation : MichelsonOriginationOperation
{
    /// <summary>Fee paid to the baker for including the operation (mutez), if it is an external one.</summary>
    public long? BakerFee { get; set; }

    /// <summary>Baker the new contract was set to delegate to, if any.</summary>
    public AddressInfo? Baker { get; set; }
}

public class XMichelsonOriginationOperation : MichelsonOriginationOperation
{
    /// <summary>Fee paid for posting the operation data to L1, based on its size (mutez), if it is an external one.</summary>
    public long? DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation was allowed to consume (mutez), if it is an external one.</summary>
    public long? GasFee { get; set; }

    /// <summary>Part of the gas fee returned to the sender for the gas that wasn't consumed (mutez), if it is an external one.</summary>
    public long? GasFeeRefunded { get; set; }
}

public class XEvmOriginationOperation : OriginationOperation
{
    /// <summary>
    /// Transaction type it was sent as (`legacy`, `dynamic_fee`, `set_code`, ...),
    /// or `trace` if it's an internal operation rather than a transaction of its own.
    /// </summary>
    public required string OpType { get; set; }

    /// <summary>EVM opcode that deployed the contract (`create` or `create2`).</summary>
    public required string OpCode { get; set; }

    /// <summary>Amount transferred to the new contract on deployment (18 decimals).</summary>
    public BigInteger Balance { get; set; }

    /// <summary>Fee paid for posting the operation data to L1, based on its size (18 decimals), if it is an external one.</summary>
    public BigInteger? DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation consumed (18 decimals), if it is an external one.</summary>
    public BigInteger? GasFee { get; set; }

    /// <summary>
    /// Gas refunded at the end of the transaction (EIP-3529), if any. Already accounted for in `gasFee`,
    /// whereas `gasUsed` is the pre-refund figure, so the two reconcile as
    /// `gasFee = effectiveGasPrice * (sum of gasUsed over the operation tree - gasRefunded)`.
    /// </summary>
    public int? GasRefunded { get; set; }

    /// <summary>Gas price the sender offered, for `legacy` and `access_list` transactions (18 decimals).</summary>
    public BigInteger? GasPrice { get; set; }

    /// <summary>Maximum total price per gas the sender agreed to pay (18 decimals).</summary>
    public BigInteger? MaxFeePerGas { get; set; }

    /// <summary>Maximum tip per gas the sender agreed to pay on top of the base fee (18 decimals).</summary>
    public BigInteger? MaxPriorityFeePerGas { get; set; }

    /// <summary>Price per gas the sender was actually charged (18 decimals).</summary>
    public BigInteger? EffectiveGasPrice { get; set; }

    /// <summary>Number of internal operations caused by the operation, if any.</summary>
    public int? InternalOperations { get; set; }

    /// <summary>Number of logs (events) emitted by the operation, if any.</summary>
    public int? LogsCount { get; set; }

    /// <summary>`true` if the contract was deployed at an address where a contract already existed.</summary>
    public bool? ReOriginated { get; set; }
}
