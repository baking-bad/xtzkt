using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(AddressHashNullBinder))]
public class AddressHashNullParameter : INormalizable
{
    /// <summary>
    /// Sentinel value used to represent a `null` filter.
    /// </summary>
    public const string Null = "";

    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'. Use `null` to get items where 'param' is not set.
    ///
    /// Example: `?from.hash=tz1...` or `?from.hash=null`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'. Use `null` to get items where 'param' is set.
    ///
    /// Example: `?from.hash.ne=tz1...` or `?from.hash.ne=null`.
    /// </summary>
    public string? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'. Use `null` to include items where 'param' is not set.
    ///
    /// Example: `?from.hash.in=tz1...,KT1...` or `?from.hash.in=tz1...,null`.
    /// </summary>
    public List<string>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'. Use `null` to exclude items where 'param' is not set.
    ///
    /// Example: `?from.hash.ni=tz1...,KT1...` or `?from.hash.ni=tz1...,null`.
    /// </summary>
    public List<string>? Ni { get; set; }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Eq}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Ne}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.OrderBy(x => x))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.OrderBy(x => x))}&");

        return sb.ToString();
    }
}
