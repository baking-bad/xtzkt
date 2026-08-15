using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtzkt.Api;

class Utf8Converter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return string.IsNullOrEmpty(str) ? null : Encoding.UTF8.GetBytes(str);
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Encoding.UTF8.GetString(value));
    }
}
