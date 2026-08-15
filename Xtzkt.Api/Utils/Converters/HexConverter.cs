using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtzkt.Api;

class HexConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrEmpty(str))
            return null;

        if (str.StartsWith("0x"))
            str = str[2..];

        return Convert.FromHexString(str);
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"0x{Convert.ToHexStringLower(value)}");
    }
}
