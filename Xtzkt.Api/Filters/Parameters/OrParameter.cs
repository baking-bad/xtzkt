using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Base;

namespace Xtzkt.Api.Filters.Parameters
{
    public class OrParameter(params (string, List<int>?)[] colsAndVals) : INormalizable
    {
        [JsonIgnore]
        public (string, List<int>?)[] ColsAndVals { get; } = colsAndVals;

        public string Normalize(string name) => $"{name}={string.Join('|', ColsAndVals
            .Where(x => x.Item2?.Count > 0)
            .Select(x => $"{x.Item1}:{string.Join(',', x.Item2!.OrderBy(x => x))}"))}&";
    }
}
