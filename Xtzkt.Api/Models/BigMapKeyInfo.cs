using System.Text.Json.Serialization;
using Netezos.Encoding;

namespace Xtzkt.Api.Models;

public class BigMapKeyInfo
{
    /// <summary>Internal unique bigmap key id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Key hash (script expression).</summary>
    public required string KeyHash { get; set; }

    /// <summary>Key in Micheline format.</summary>
    public required IMicheline RawKey { get; set; }

    /// <summary>Key in JSON format.</summary>
    public required RawJson Key { get; set; }
}
