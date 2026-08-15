using System.Text.Json.Serialization;
using Netezos.Encoding;

namespace Xtzkt.Api.Models;

public class BigMapUpdate
{
    /// <summary>Internal unique bigmap update id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the bigmap update belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Bigmap that was updated.</summary>
    public required BigMapInfo BigMap { get; set; }

    /// <summary>Level of the block where the update happened.</summary>
    public int Level { get; set; }

    /// <summary>Timestamp of the block where the update happened.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Action (`allocate`, `add_key`, `update_key`, `remove_key` or `remove`).</summary>
    public required string Action { get; set; }

    /// <summary>Bigmap key that was updated (`null` for `allocate` and `remove` actions).</summary>
    public BigMapKeyInfo? BigMapKey { get; set; }

    /// <summary>Value that was set in Micheline format (`null` if there was no value set).</summary>
    public IMicheline? RawValue { get; set; }

    /// <summary>Value that was set in JSON format (`null` if there was no value set).</summary>
    public RawJson? Value { get; set; }

    /// <summary>Id of the transaction operation, caused the update (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? TransactionId { get; set; }

    /// <summary>Id of the origination operation, caused the update (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? OriginationId { get; set; }

    /// <summary>Id of the migration, caused the update (if any).</summary>
    [JsonConverter(typeof(Int64StringNullableConverter))]
    public long? MigrationId { get; set; }
}
