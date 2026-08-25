using System.Numerics;
using System.Text.Json.Serialization;
using Xtzkt.Api.Models.Abstract;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Models.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "runtime")]
[JsonDerivedType(typeof(MichelsonMigrationOperation), Runtimes.Michelson)]
[JsonDerivedType(typeof(EvmMigrationOperation), Runtimes.Evm)]
public abstract class MigrationOperation : IActivity
{
    /// <summary>Internal unique operation id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the migration happened on.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Level of the block the migration happened in.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block the migration happened in.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Why the migration happened (`bootstrap`, `air_drop`, `code_change`, ...).</summary>
    public required string Kind { get; set; }

    /// <summary>Address affected by the migration.</summary>
    public required AddressInfo Account { get; set; }
}

public class MichelsonMigrationOperation : MigrationOperation
{
    /// <summary>How much the address balance changed, if at all (mutez).</summary>
    public long BalanceChange { get; set; }

    /// <summary>Number of token transfers caused by the migration, if any.</summary>
    public int? TokenTransfers { get; set; }

    /// <summary>Number of bigmap updates caused by the migration, if any.</summary>
    public int? BigMapUpdates { get; set; }
}

public class EvmMigrationOperation : MigrationOperation
{
    /// <summary>How much the address balance changed, if at all (18 decimals).</summary>
    public BigInteger BalanceChange { get; set; }

    /// <summary>How much the address nonce changed, if at all.</summary>
    public int NonceChange { get; set; }
}
