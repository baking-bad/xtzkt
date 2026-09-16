namespace Xtzkt.Api;

/// <summary>
/// An inclusive range of entity ids.
/// </summary>
public readonly record struct IdRange(long Min, long Max);
