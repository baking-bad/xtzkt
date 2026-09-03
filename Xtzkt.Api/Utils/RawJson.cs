using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xtzkt.Api
{
    [JsonConverter(typeof(RawJsonConverter))]
    public class RawJson(string json)
    {
        string Json { get; } = json;

        public static implicit operator RawJson? (string? value) => value is null ? null : new(value);
        public static implicit operator string? (RawJson? value) => value?.Json;

        public override string ToString() => Json;
    }

    class RawJsonConverter : JsonConverter<RawJson>
    {
        public override RawJson? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.GetRawText();
        }

        public override void Write(Utf8JsonWriter writer, RawJson value, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.Parse(value.ToString(), new JsonDocumentOptions { MaxDepth = 100_000 });
            doc.WriteTo(writer);
        }
    }
}
