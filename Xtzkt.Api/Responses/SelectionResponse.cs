using System.Text.Json.Serialization;

namespace Xtzkt.Api.Responses;

[JsonConverter(typeof(SelectionConverter))]
public class SelectionResponse
{
    public string[]? Cols { get; set; }
    public required object?[][] Rows { get; set; }
}
