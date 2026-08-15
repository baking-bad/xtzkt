using Netezos.Encoding;

namespace Xtzkt.Api.Models;

public class BigMap
{
    /// <summary>Internal unique bigmap id.</summary>
    public int Id { get; set; }

    /// <summary>Chain the bigmap belongs to.</summary>
    public required ChainInfo Chain { get; set; }

    /// <summary>Bigmap pointer, also known as bigmap id.</summary>
    public int Ptr { get; set; }

    /// <summary>Contract the bigmap belongs to.</summary>
    public required ContractInfo Contract { get; set; }

    /// <summary>Path to the bigmap in the contract storage.</summary>
    public required string StoragePath { get; set; }

    /// <summary>Whether the bigmap is allocated (`true`) or removed (`false`).</summary>
    public bool Active { get; set; }

    /// <summary>Bigmap key type in Micheline format.</summary>
    public required IMicheline KeyType { get; set; }

    /// <summary>Bigmap value type in Micheline format.</summary>
    public required IMicheline ValueType { get; set; }

    /// <summary>Level of the block where the bigmap was allocated.</summary>
    public int FirstLevel { get; set; }

    /// <summary>Timestamp of the block where the bigmap was allocated.</summary>
    public DateTime FirstTimestamp { get; set; }

    /// <summary>Level of the block where the bigmap was last updated.</summary>
    public int LastLevel { get; set; }

    /// <summary>Timestamp of the block where the bigmap was last updated.</summary>
    public DateTime LastTimestamp { get; set; }

    /// <summary>Total number of keys ever added to the bigmap.</summary>
    public int TotalKeys { get; set; }

    /// <summary>Number of active (non-removed) keys.</summary>
    public int ActiveKeys { get; set; }

    /// <summary>Total number of updates.</summary>
    public int Updates { get; set; }

    /// <summary>List of tags (`persistent`, `metadata`, `token_metadata`, `ledger`).</summary>
    public List<string> Tags { get; set; } = [];
}
