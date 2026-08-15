using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(ActivityTypesBinder))]
public class ActivityTypesParameter : INormalizable
{
    [JsonIgnore]
    public HashSet<string>? Types { get; set; }

    public string Normalize(string name)
    {
        return Types != null
            ? $"{name}={string.Join(',', Types.OrderBy(x => x))}&"
            : string.Empty;
    }
}
