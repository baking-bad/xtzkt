using System.Text.Json.Serialization;
using Netezos.Encoding;

namespace Xtzkt.Api.Models;

public class BigMapKey
{
    /// <summary>Internal unique bigmap key id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the bigmap key belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Bigmap the key belongs to.</summary>
    public required BigMapInfo BigMap { get; set; }

    /// <summary>Whether the key is active (`true`) or removed (`false`).</summary>
    public bool Active { get; set; }

    /// <summary>Key hash (script expression).</summary>
    public required string KeyHash { get; set; }

    /// <summary>Key in Micheline format.</summary>
    public required IMicheline RawKey { get; set; }

    /// <summary>Key in JSON format.</summary>
    public required RawJson Key { get; set; }

    /// <summary>Value in Micheline format.</summary>
    public required IMicheline RawValue { get; set; }

    /// <summary>Value in JSON format.</summary>
    public required RawJson Value { get; set; }

    /// <summary>Level of the block where the key was first seen.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the key was first seen.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the key was last updated.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the key was last updated.</summary>
    public DateTime LastTimestamp { get; set; }

    /// <summary>Total number of updates.</summary>
    public int Updates { get; set; }
}
