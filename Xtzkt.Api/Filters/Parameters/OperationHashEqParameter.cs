using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(OperationHashEqBinder))]
public class OperationHashEqParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?hash=o...` or `?hash=0x...`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?hash.in=o...,0x...`.
    /// </summary>
    public List<string>? In { get; set; }

    public OperationHashParameter ToOperationHashParameter() => new() { Eq = Eq, In = In };

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
