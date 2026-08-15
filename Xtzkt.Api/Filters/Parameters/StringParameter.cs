using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(StringBinder))]
public class StringParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?entrypoint=transfer`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?entrypoint.ne=transfer`.
    /// </summary>
    public string? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?entrypoint.in=transfer,approve`.
    /// </summary>
    public List<string>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?entrypoint.ni=transfer,approve`.
    /// </summary>
    public List<string>? Ni { get; set; }

    public static implicit operator StringParameter(string value) => new() { Eq = value };

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Uri.EscapeDataString(Eq)}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Uri.EscapeDataString(Ne)}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.OrderBy(x => x).Select(Uri.EscapeDataString))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.OrderBy(x => x).Select(Uri.EscapeDataString))}&");

        return sb.ToString();
    }
}
