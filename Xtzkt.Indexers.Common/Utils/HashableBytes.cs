using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.Common.Utils;

public class HashableBytes(byte[] bytes)
{
    public byte[] Bytes { get; } = bytes;

    public static implicit operator HashableBytes(byte[] array) => new(array);
    public override bool Equals(object? obj) => obj is HashableBytes hb && hb.Bytes.IsEqual(Bytes);
    public override int GetHashCode() => Bytes.GetHashCodeExt();
    public override string ToString() => Netezos.Encoding.Hex.Convert(Bytes);

    public static HashableBytes? From(byte[]? array) => array == null ? null : new(array);
}
