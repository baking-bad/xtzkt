using System.Numerics;
using System.Text.Json.Serialization;
using Netezos.Encoding;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "layer")]
[JsonDerivedType(typeof(L1TransferTicketOperation), Layers.L1)]
[JsonDerivedType(typeof(XTransferTicketOperation), Layers.TezosX)]
public abstract class TransferTicketOperation : IOpgActivity, ITicketTransfersSource
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

    /// <summary>Address that sent the tickets.</summary>
    public required AddressInfo Sender { get; set; }

    /// <summary>Address the tickets were sent to.</summary>
    public required AddressInfo Target { get; set; }

    /// <summary>Contract that issued the tickets.</summary>
    public required AddressInfo Ticketer { get; set; }

    /// <summary>Number of tickets sent.</summary>
    public BigInteger Amount { get; set; }

    /// <summary>Target's entrypoint the tickets were sent to.</summary>
    public required string Entrypoint { get; set; }

    /// <summary>Ticket content in JSON format.</summary>
    public RawJson? Content { get; set; }

    /// <summary>Ticket content in Micheline format.</summary>
    public IMicheline? ContentRaw { get; set; }

    /// <summary>Ticket content type in Micheline format.</summary>
    public IMicheline? TypeRaw { get; set; }

    /// <summary>Sender's operation counter, ensuring operations apply in order and only once.</summary>
    public int Counter { get; set; }

    /// <summary>Amount burned for the storage the operation used (mutez).</summary>
    public long? StorageFee { get; set; }

    /// <summary>Maximum gas the sender allowed the operation to consume.</summary>
    public int GasLimit { get; set; }

    /// <summary>Gas the operation actually consumed.</summary>
    public int GasUsed { get; set; }

    /// <summary>Maximum storage the sender allowed the operation to allocate (bytes).</summary>
    public int StorageLimit { get; set; }

    /// <summary>Storage the operation actually allocated (bytes).</summary>
    public int StorageUsed { get; set; }

    /// <summary>Operation status (`applied`, `failed`, `backtracked`, `skipped`).</summary>
    public required string Status { get; set; }

    /// <summary>Errors the operation failed with, if it wasn't applied.</summary>
    public string? Errors { get; set; }

    /// <summary>Number of ticket transfers caused by the operation, if any.</summary>
    public int? TicketTransfers { get; set; }

    /// <summary>Number of internal operations caused by the operation, if any.</summary>
    public int? InternalOperations { get; set; }
}

public class L1TransferTicketOperation : TransferTicketOperation
{
    /// <summary>Fee paid to the baker for including the operation (mutez).</summary>
    public long BakerFee { get; set; }
}

public class XTransferTicketOperation : TransferTicketOperation
{
    /// <summary>Fee paid for posting the operation data to L1, based on its size (mutez).</summary>
    public long DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation was allowed to consume (mutez).</summary>
    public long GasFee { get; set; }

    /// <summary>Part of the gas fee returned to the sender for the gas that wasn't consumed (mutez).</summary>
    public long GasRefund { get; set; }
}
