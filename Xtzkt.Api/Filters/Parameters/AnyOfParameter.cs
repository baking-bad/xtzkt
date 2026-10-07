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
    /// The value is an internal address id (default, so `.id` may be omitted), or `null` to match items
    /// where any of the fields is not set. To specify an address hash instead, use the `.hash` form,
    /// which matches that hash on every chain it exists on and accepts `null` the same way.
    ///
    /// Examples: `?anyof.sender.target=123`, `?anyof.sender.target.hash=tz1...`.
    /// </summary>
    public int? Eq { get; set; }

    /// <summary>
    /// **In list** (any of) filter mode.
    /// Specify a comma-separated list of values to get items where any of the specified fields is equal to one of the specified values.
    ///
    /// Examples: `?anyof.sender.target.in=123,456,null`, `?anyof.sender.target.hash.in=tz1...,KT1...,null`.
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
