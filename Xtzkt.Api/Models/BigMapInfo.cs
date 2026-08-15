namespace Xtzkt.Api.Models;

public class BigMapInfo
{
    /// <summary>Internal unique bigmap id.</summary>
    public int Id { get; set; }

    /// <summary>Bigmap pointer, also known as bigmap id.</summary>
    public int Ptr { get; set; }

    /// <summary>Contract the bigmap belongs to.</summary>
    public required ContractInfo Contract { get; set; }

    /// <summary>Path to the bigmap in the contract storage.</summary>
    public required string StoragePath { get; set; }
}
