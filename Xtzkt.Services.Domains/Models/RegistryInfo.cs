namespace Xtzkt.Services.Domains.Models;

public sealed record RegistryInfo(
    int Id,
    int ChainId,
    string Hash,
    int RecordsBigMap,
    int ReverseBigMap,
    int ExpiryBigMap);
