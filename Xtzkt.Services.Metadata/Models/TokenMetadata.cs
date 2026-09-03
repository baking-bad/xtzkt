using Xtzkt.Data.Models;

namespace Xtzkt.Services.Metadata.Models;

public sealed record TokenMetadata(
    long Id,
    TokenMetadataStatus Status,
    DateTime? SyncedAt,
    string? Name = null,
    string? Symbol = null,
    int? Decimals = null,
    string? Json = null,
    string? Link = null)
{
    public TokenMetadataStatus Status { get; set; } = Status;
}

public sealed record TokenMetadataEx(
    int ContractId,
    string TokenId,
    TokenMetadataStatus Status,
    DateTime? SyncedAt,
    string? Name = null,
    string? Symbol = null,
    int? Decimals = null,
    string? Json = null,
    string? Link = null);