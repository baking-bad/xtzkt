using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(BlockHashBinder))]
public class BlockHashParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?hash=B...` or `?hash=0x...`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?hash.ne=B...`.
    /// </summary>
    public string? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?hash.in=B...,0x...`.
    /// </summary>
    public List<string>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?hash.ni=B...,0x...`.
    /// </summary>
    public List<string>? Ni { get; set; }

    public static implicit operator BlockHashParameter(string value) => new() { Eq = value };

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
