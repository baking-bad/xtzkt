using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtzkt.Api;

class Int64StringNullableConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (long.TryParse(reader.GetString(), out var res))
                return res;
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var int64))
                return int64;
        }
        else if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        throw new JsonException("Failed to parse Int64? value");
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is long int64)
            writer.WriteStringValue(int64.ToString());
        else
            writer.WriteNullValue();
    }
}
