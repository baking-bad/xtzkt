using System.Text.Json.Serialization;
using Netezos.Encoding;

namespace Xtzkt.Api.Models;

public class Storage
{
    /// <summary>Internal unique storage id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the storage belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Contract the storage belongs to.</summary>
    public required ContractInfo Contract { get; set; }

    /// <summary>Level of the block where the storage was set.</summary>
    public int Level { get; set; }

    /// <summary>Whether this is the current storage of the contract.</summary>
    public bool Current { get; set; }

    /// <summary>Storage value in Micheline format.</summary>
    public required IMicheline RawValue { get; set; }

    /// <summary>Storage value in JSON format.</summary>
    public required RawJson Value { get; set; }

    /// <summary>Id of the transaction operation, set the storage (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? TransactionId { get; set; }

    /// <summary>Id of the origination operation, set the storage (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? OriginationId { get; set; }

    /// <summary>Id of the migration, set the storage (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? MigrationId { get; set; }
}
