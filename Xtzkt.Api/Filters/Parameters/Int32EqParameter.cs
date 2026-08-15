using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(Int32EqBinder))]
public class Int32EqParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?level=123`.
    /// </summary>
    public int? Eq { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?level.in=123,456`.
    /// </summary>
    public List<int>? In { get; set; }

    public Int32Parameter ToInt32Parameter() => new() { Eq = Eq, In = In };

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Eq}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.OrderBy(x => x))}&");

        return sb.ToString();
    }
}
