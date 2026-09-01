using Xtzkt.Utils.Encoding;

namespace Xtzkt.Indexers.Common.Utils;

public readonly struct HashKey(byte[] bytes) : IEquatable<HashKey>
{
    public byte[] Bytes { get; } = bytes;

    public bool Equals(HashKey other) => Bytes.AsSpan().SequenceEqual(other.Bytes);

    public override bool Equals(object? obj) => obj is HashKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Bytes);
        return hash.ToHashCode();
    }

    public override string ToString() => Hex.GetString(Bytes);

    public static bool operator ==(HashKey left, HashKey right) => left.Equals(right);

    public static bool operator !=(HashKey left, HashKey right) => !left.Equals(right);

    public static implicit operator HashKey(byte[] bytes) => new(bytes);


    public static HashKey? From(byte[]? bytes) => bytes == null ? (HashKey?)null : new HashKey(bytes);
}
