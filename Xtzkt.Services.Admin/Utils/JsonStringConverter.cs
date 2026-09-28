using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtzkt.Services.Admin.Utils;

class JsonStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value?.Contains('\0') == true)
            throw new JsonException("NUL characters are not allowed");

        return value;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
