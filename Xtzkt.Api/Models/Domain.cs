using System.Text.Json.Serialization;

namespace Xtzkt.Api.Models;

public class Domain
{
    /// <summary>Internal unique domain id.</summary>
    [JsonConverter(typeof(Int64StringConverter))]
    public long Id { get; set; }

    /// <summary>Chain the domain belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Name registry contract the domain is registered in.</summary>
    public required AddressInfo Registry { get; set; }

    /// <summary>Domain level: `1` for a top-level domain like `tez`, `2` for `alice.tez`, `3` for `sub.alice.tez`, and so on.</summary>
    public int Level { get; set; }

    /// <summary>Domain name, e.g. `alice.tez`.</summary>
    public required string Name { get; set; }

    /// <summary>Address that owns the domain.</summary>
    public required string Owner { get; set; }

    /// <summary>Address the domain resolves to, if any.</summary>
    public string? Address { get; set; }

    /// <summary>Whether the domain is the primary name of its `address`, i.e. the reverse record of the address points to it.</summary>
    public bool Reverse { get; set; }

    /// <summary>Expiration time, or `9999-12-31T00:00:00Z` if the domain doesn't expire.</summary>
    public DateTime Expiration { get; set; }

    /// <summary>Custom domain data, like `openid:name` or `twitter:handle`, with values decoded from the stored bytes.</summary>
    public RawJson? Data { get; set; }

    /// <summary>Level of the block where the domain was first seen.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the domain was first seen.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the domain, its expiration or the reverse record of its address was last updated.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the domain, its expiration or the reverse record of its address was last updated.</summary>
    public DateTime LastTimestamp { get; set; }
}
