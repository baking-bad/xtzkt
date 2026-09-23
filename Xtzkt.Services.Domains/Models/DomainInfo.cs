namespace Xtzkt.Services.Domains.Models;

/// <summary>
/// A parsed domain, ready to be written.
/// </summary>
public sealed record DomainInfo(
    long Id,
    int Level,
    string Name,
    string Owner,
    string? Address,
    bool Reverse,
    DateTime Expiration,
    string? Data,
    int FirstLevel,
    int LastLevel);

/// <summary>
/// A row of the `records` big map, as it comes out of the DB.
/// </summary>
public sealed record DomainRecord(
    long Id,
    int FirstLevel,
    int LastLevel,
    int MaxLastLevel,
    string? NameHex,
    string? Level,
    string? Owner,
    string? Address,
    string? Data,
    DateTime Expiration,
    bool Reverse);
