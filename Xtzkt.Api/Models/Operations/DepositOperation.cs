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
}

public class XMichelsonDepositOperation : DepositOperation
{
    /// <summary>Deposited amount (mutez).</summary>
    public long Amount { get; set; }
}

public class XEvmDepositOperation : DepositOperation
{
    /// <summary>Deposited amount (18 decimals).</summary>
    public BigInteger Amount { get; set; }

    /// <summary>Hash of the L1 ticket backing the deposited token (`fa` deposits only).</summary>
    [JsonConverter(typeof(HexConverter))]
    public byte[]? TicketHash { get; set; }

    /// <summary>ERC20 proxy contract the deposited token is exposed as (`fa` deposits only).</summary>
    public AddressInfo? Proxy { get; set; }

    /// <summary>
    /// Queue id of the deposit. Set when the deposit was queued instead of being credited right away —
    /// the funds stay on the bridge until it's claimed with this id.
    /// </summary>
    public BigInteger? DepositId { get; set; }
}
