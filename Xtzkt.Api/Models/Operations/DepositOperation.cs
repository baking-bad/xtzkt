using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "runtime")]
[JsonDerivedType(typeof(XMichelsonDepositOperation), Runtimes.Michelson)]
[JsonDerivedType(typeof(XEvmDepositOperation), Runtimes.Evm)]
public abstract class DepositOperation : IOpgActivity
{
    /// <summary>Internal unique operation id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the deposit was credited on.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Level of the block the deposit was credited in.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block the deposit was credited in.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Hash of the operation.</summary>
    public required string Hash { get; set; }

    /// <summary>Operation status (`applied`, `failed`, ...).</summary>
    public required string Status { get; set; }

    /// <summary>Level of the L1 block the deposit message was posted to the rollup inbox in.</summary>
    public int InboxLevel { get; set; }

    /// <summary>Index of the deposit message within the rollup inbox at that level.</summary>
    public int InboxMessageId { get; set; }

    /// <summary>Address the deposit was credited to.</summary>
    public required AddressInfo Receiver { get; set; }

    /// <summary>What was deposited: `xtz` for native tez, `fa` for an FA token.</summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gas consumed by the deposit. Informational only — the kernel credits deposits for free,
    /// so this gas is not charged to anyone, but it does count towards the block's gas usage.
    /// </summary>
    public int GasUsed { get; set; }
}

public class XMichelsonDepositOperation : DepositOperation
{
    /// <summary>Deposited amount (mutez).</summary>
    public long Amount { get; set; }
}

public class XEvmDepositOperation : DepositOperation, IBridgeTicketTransfersSource
{
    /// <summary>
    /// Deposited amount: 18 decimals for `xtz`, ticket units for `fa` — the bridge credits FA tickets
    /// one to one, so an `fa` amount is not scaled.
    /// </summary>
    public BigInteger Amount { get; set; }

    /// <summary>
    /// Hash of the L1 ticket backing the deposited token (`fa` deposits only). It's the same hash
    /// `/v1/bridge_tickets` and `/v1/tickets` expose as `weakHash`, so it links the deposit to the
    /// bridge ticket it credits and to the L1 ticket behind it.
    /// </summary>
    [JsonConverter(typeof(HexConverter))]
    public byte[]? TicketHash { get; set; }

    /// <summary>ERC20 proxy contract the deposited token is exposed as (`fa` deposits only).</summary>
    public AddressInfo? Proxy { get; set; }

    /// <summary>
    /// Queue nonce of the deposit — the id the bridge's claim entrypoint takes, not an entity id
    /// (that's `id`). Set when the deposit was queued instead of being credited right away, and the
    /// funds stay on the bridge until it's claimed with this nonce. Mind that the two bridges number
    /// their queues independently, so a nonce is only unique together with `type`.
    /// </summary>
    public BigInteger? DepositId { get; set; }

    /// <summary>
    /// Id of the transaction that claimed the deposit off the queue. A deposit with a `depositId`
    /// and no `claimTransactionId` is still queued, waiting to be claimed.
    /// </summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? ClaimTransactionId { get; set; }

    /// <summary>Number of logs (events) emitted by the operation, if any.</summary>
    public int? LogsCount { get; set; }

    /// <summary>Number of bridge ticket transfers caused by the operation, if any.</summary>
    public int? BridgeTicketTransfers { get; set; }
}
