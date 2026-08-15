using Netezos.Encoding;

namespace Xtzkt.Api.Utils;

/// <summary>
/// Null-safe converters of raw DB values into their API representation.
/// </summary>
static class Decode
{
    public static string? ToHex(byte[]? bytes) => bytes == null ? null : Xtzkt.Utils.Encoding.Hex.GetString(bytes);

    public static string? ToUtf8(byte[]? bytes) => bytes == null ? null : Xtzkt.Utils.Encoding.Utf8.GetString(bytes);

    public static IMicheline? ToMicheline(byte[]? bytes) => bytes == null ? null : Micheline.FromBytes(bytes);
}
