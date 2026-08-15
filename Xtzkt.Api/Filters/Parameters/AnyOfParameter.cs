using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json.Serialization;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(AnyOfBinder))]
public class AnyOfParameter : INormalizable
{
    /// <summary>
    /// **Equal** filter mode (optional, i.e. `param.eq=123` is the same as `param=123`).
    /// Specify a value to get items where any of the specified fields is equal to the specified value.
    ///
    /// Example: `?anyof.sender.target=tz1...`.
    /// </summary>
    public int? Eq { get; set; }

    /// <summary>
    /// **In list** (any of) filter mode.
    /// Specify a comma-separated list of values to get items where any of the specified fields is equal to one of the specified values.
    ///
    /// Example: `?anyof.sender.target.in=tz1...,KT1...,null`.
    /// </summary>
    public List<int>? In { get; set; }

    [JsonIgnore]
    public IEnumerable<string> Fields { get; set; } = [];

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.{string.Join(".", Fields.OrderBy(x => x))}.eq={Eq}&");

        if (In?.Count > 0)
            sb.Append($"{name}.{string.Join(".", Fields.OrderBy(x => x))}.in={string.Join(",", In.OrderBy(x => x))}&");

        return sb.ToString();
    }
}
