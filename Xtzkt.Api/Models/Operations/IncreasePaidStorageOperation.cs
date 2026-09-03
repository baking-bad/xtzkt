using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "layer")]
[JsonDerivedType(typeof(L1IncreasePaidStorageOperation), Layers.L1)]
[JsonDerivedType(typeof(XIncreasePaidStorageOperation), Layers.TezosX)]
public abstract class IncreasePaidStorageOperation : IOpgActivity
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

    /// <summary>Address that paid for the extra storage.</summary>
    public required AddressInfo Sender { get; set; }

    /// <summary>Contract the storage was paid for.</summary>
    public required AddressInfo Contract { get; set; }

    /// <summary>Number of bytes the contract's paid storage was increased by.</summary>
    public BigInteger Amount { get; set; }

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
}

public class L1IncreasePaidStorageOperation : IncreasePaidStorageOperation
{
    /// <summary>Fee paid to the baker for including the operation (mutez).</summary>
    public long BakerFee { get; set; }
}

public class XIncreasePaidStorageOperation : IncreasePaidStorageOperation
{
    /// <summary>Fee paid for posting the operation data to L1, based on its size (mutez).</summary>
    public long DaFee { get; set; }

    /// <summary>Fee paid for the gas the operation was allowed to consume (mutez).</summary>
    public long GasFee { get; set; }

    /// <summary>Part of the gas fee returned to the sender for the gas that wasn't consumed (mutez).</summary>
    public long GasFeeRefunded { get; set; }
}
