using System.Text.Json;
using System.Text.Json.Serialization;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Api;

class HexListConverter : JsonConverter<List<byte[]>>
{
    public override List<byte[]>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            return null;

        var res = new List<byte[]>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            res.Add(Hex.GetBytes(reader.GetString()!));

        return res;
    }

    public override void Write(Utf8JsonWriter writer, List<byte[]> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(Hex.GetString(item));
        writer.WriteEndArray();
    }
}
