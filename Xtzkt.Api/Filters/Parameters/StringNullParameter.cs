using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(StringNullBinder))]
public class StringNullParameter : INormalizable
{
    /// <summary>
    /// Sentinel value used to represent a `null` filter.
    /// </summary>
    public const string Null = "ъуъ";

    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'. Use `null` to get items where 'param' is not set.
    ///
    /// Example: `?entrypoint=transfer` or `?entrypoint=null`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'. Use `null` to get items where 'param' is set.
    ///
    /// Example: `?entrypoint.ne=transfer` or `?entrypoint.ne=null`.
    /// </summary>
    public string? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'. Use `null` to include items where 'param' is not set.
    ///
    /// Example: `?entrypoint.in=transfer,approve` or `?entrypoint.in=transfer,null`.
    /// </summary>
    public List<string>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'. Use `null` to exclude items where 'param' is not set.
    ///
    /// Example: `?entrypoint.ni=transfer,approve` or `?entrypoint.ni=transfer,null`.
    /// </summary>
    public List<string>? Ni { get; set; }

    public static implicit operator StringNullParameter(string? value) => new() { Eq = value ?? Null };

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
