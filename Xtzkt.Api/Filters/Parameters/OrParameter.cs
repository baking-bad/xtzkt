using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Base;

namespace Xtzkt.Api.Filters.Parameters
{
    public class OrParameter(params (string[] columns, List<int> values)[] colsAndVals) : INormalizable
    {
        [JsonIgnore]
        public (string[] columns, List<int> values)[] ColsAndVals { get; } = colsAndVals;

        public string Normalize(string name) => $"{name}={string.Join('|', ColsAndVals
            .Where(x => x.values.Count > 0)
            .Select(x => $"{string.Join('+', x.columns)}:{string.Join(',', x.values.Order())}"))}&";
    }
}
